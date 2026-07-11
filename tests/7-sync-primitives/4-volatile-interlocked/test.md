# Задача: Высокопроизводительный счётчик посещений с lock-free алгоритмами

## Условие

Вы пишете систему аналитики для high-traffic веб-сайта. Нужно считать посещения, поддерживать « trending» товары (топ по просмотрам) и управлять флагами остановки/перезапуска воркеров — всё без блокировок (lock-free), используя `Interlocked` и `volatile`.

### Требования

1. **Класс `VisitCounter`** — атомарный счётчик посещений.

2. **Метод `DemonstrateNonAtomicCounter()`** — показать, почему обычный инкремент ломается:
   - 10 потоков, каждый инкрементит `_counter++` 100,000 раз
   - Результат: < 1,000,000 (потеря инкрементов)
   - Вывести потерю в процентах

3. **Метод `InterlockedIncrementCounter()`** — атомарный счётчик:
   - `Interlocked.Increment(ref _counter)` — атомарно
   - Результат: ровно 1,000,000
   - Замерить время

4. **Метод `InterlockedCompareExchangeMax()`** — trending-товары через CAS:
   - Есть `_topViews` — максимальное количество просмотров товара
   - Каждый поток «просматривает» случайный товар с случайным количеством просмотров
   - Нужно атомарно обновить `_topViews`, если новое значение больше
   - Использовать CAS-цикл: `Interlocked.CompareExchange` в цикле `do/while`
   - Показать: финальное `_topViews` — действительно максимум

5. **Метод `InterlockedExchangeExample()`** — переключение активного воркера:
   - `_activeWorker` — ссылка на текущий активный воркер
   - Атомарно заменить на другого через `Interlocked.Exchange`
   - Старый воркер получить для cleanup

6. **Метод `VolatileFlagDemo()`** — управление воркерами через volatile:
   - `volatile bool _shouldStop` — флаг для graceful shutdown
   - Один поток крутится в цикле `while (!_shouldStop)`
   - Другой поток выставляет `_shouldStop = true`
   - **Без volatile**: компилятор/CPU могут переупорядочить операции, и поток может НИКОГДА не увидеть изменение флага
   - **С volatile**: гарантирует видимость изменения между потоками

7. **Метод `VolatileReadWriteExample()`** — альтернатива volatile:
   - То же самое, но с `Volatile.Read(ref flag)` и `Volatile.Write(ref flag, true)`
   - Показать, что это эквивалент volatile для произвольных переменных

8. **Сводное сравнение** — таблица «что когда использовать»:
   - volatile: простые флаги между потоками
   - Interlocked: атомарные операции (increment, swap, CAS)
   - lock: сложные критические секции из нескольких операций

### Ожидаемый вывод

```
=== Non-Atomic Counter ===
Expected: 1,000,000
Actual:   843,291
Lost: 156,709 (15.7%)

=== Interlocked.Increment ===
Expected: 1,000,000
Actual:   1,000,000
Time: 45ms

=== Interlocked.CompareExchange (CAS) — Top Views ===
[Thread 3] New high score: 950 views (old: 920)
[Thread 5] New high score: 998 views (old: 950)
...
Final top views: 999 (correct maximum!)

=== Interlocked.Exchange — Worker Swap ===
Old worker: Worker-A (requests: 1523)
Swapped to: Worker-B
Old worker disposed.

=== Volatile Flag ===
[Worker] Started, waiting for stop signal...
[Main]   Setting stop flag...
[Worker] Stop signal received! Shutting down.

Without volatile — worker might run forever due to CPU caching/reordering.

=== Volatile.Read/Write ===
Same as volatile, but for any variable via explicit barriers.
```

### Ограничения
- Не использовать `lock` в этой задаче — только volatile и Interlocked
- Для CAS-цикла: `do { original = value; result = ...; } while (Interlocked.CompareExchange(...) != original)`
- Для volatile: показать объявление поля с `volatile` и использование `Volatile.Read/Write`
- Для демонстрации volatile — добавить комментарий, почему без volatile может «зависнуть»

# Задача: Высоконагруженный кэш сессий пользователей — Task vs ValueTask benchmark

## Условие

Вы оптимизируете кэш сессий для high-load веб-сервиса (миллионы запросов в секунду). Профилировщик показал, что 40% аллокаций приходится на `Task<Session>` при каждом обращении к кэшу. Ваша задача: заменить `Task<T>` на `ValueTask<T>` и измерить выигрыш. Также показать ограничения `ValueTask`.

### Требования

1. **Класс `SessionCache`** реализует кэш с двумя версиями API: на `Task<T>` и на `ValueTask<T>`.

2. **Метод `GetSessionWithTask(int userId)`** — возвращает `Task<Session>`:
   - Если сессия в кэше — сразу возвращает её (синхронно, но всё равно аллокация Task)
   - Если сессии нет — «идёт в БД» через `await Task.Delay(100)` и возвращает новую сессию

3. **Метод `GetSessionWithValueTask(int userId)`** — возвращает `ValueTask<Session>`:
   - Если сессия в кэше — возвращает напрямую (без аллокаций в heap!)
   - Если сессии нет — оборачивает Task в ValueTask

4. **Метод `RunBenchmark(int iterations)`** — сравнивает Task vs ValueTask:
   - Запускает `iterations` вызовов (например, 1,000,000) для КАЖДОГО варианта
   - Измеряет: время выполнения (`Stopwatch`), аллокации (`GC.GetTotalMemory` до и после, с `GC.Collect` между тестами)
   - В кэш попадает 95% запросов (синхронное завершение), 5% — промахи (асинхронное)
   - Выводит: время, разницу в аллокациях, speedup

5. **Метод `DemonstrateValueTaskLimitations()`** — показывает ограничения:
   - **Нельзя await дважды**: создать `ValueTask`, await его, попробовать await ещё раз → `InvalidOperationException`
   - **Нельзя WhenAll**: попытаться передать `ValueTask` в `Task.WhenAll` → ошибка компиляции (показать, как обойти через `.AsTask()`)
   - **AsTask() создаёт аллокацию**: показать, что `.AsTask()` нивелирует выигрыш ValueTask

6. **Метод `ShowMemoryUsage()`** — выводит сравнение:
   - Размер `Task<int>` в памяти (~48 байт)
   - Размер `ValueTask<int>` в памяти (~8 байт на стеке)
   - Экономия при 1,000,000 вызовов

### Ожидаемый вывод

```
=== BENCHMARK: 1,000,000 sessions | Cache hit rate: 95% ===
Warming up...

--- Task<Session> ---
  Time: 245ms
  Allocations: 48,000 KB (~48 MB)
  GC Collections (Gen0): 12

--- ValueTask<Session> ---
  Time: 95ms (speedup: 2.6x)
  Allocations: 2,400 KB (~2.4 MB)
  GC Collections (Gen0): 1
  Memory saved: 45,600 KB (~95% savings!)

=== ValueTask Limitations ===

1. Double await:
   ValueTask<int> vt = GetValueAsync();
   await vt; // OK
   await vt; // InvalidOperationException: ValueTask consumed twice!
   Fix: Don't await twice, or use .AsTask() (but that allocates).

2. WhenAll:
   // Error: Task.WhenAll requires Task, not ValueTask
   await Task.WhenAll(vt1.AsTask(), vt2.AsTask()); // allocates!

3. AsTask() penalty:
   AsTask() creates a new Task wrapper — loses the ValueTask advantage.

=== Memory comparison ===
| Type        | Size per item | 1M calls total |
|-------------|---------------|----------------|
| Task<T>     | ~48 bytes     | ~48 MB          |
| ValueTask<T>| ~8 bytes      | ~8 MB (stack)   |

Rule: Use ValueTask when method completes synchronously >90% of the time.
```

### Ограничения
- Для измерения аллокаций: `GC.Collect()`, `GC.WaitForPendingFinalizers()`, `GC.Collect()` перед каждым тестом
- Кэш: `ConcurrentDictionary<int, Session>`
- 95% cache hit rate
- Показать double-await exception

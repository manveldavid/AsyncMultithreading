# Задача: Дебаггер deadlock-ов в системе бронирования билетов

## Условие

Вы — разработчик в компании по продаже билетов. Система работает на WPF-десктопе. В production-е обнаружились подвисания: при нажатии кнопки «Забронировать» приложение намертво зависает. Ваша задача: воспроизвести deadlock, понять его причину и применить ВСЕ 4 способа исправления.

### Требования

1. **Эмуляция UI-контекста**: Поскольку мы в консоли, нужно создать кастомный `SynchronizationContext`, который эмулирует поведение WPF (один выделенный поток с очередью). Все deadlock-и должны происходить ТОЛЬКО внутри этого контекста (как в реальном WPF).

2. **Класс `TicketBookingSystem`** — реализует 5 сценариев:

3. **Сценарий 1: КЛАССИЧЕСКИЙ DEADLOCK** — метод `DeadlockWithWait()`:
   - Запускается внутри кастомного SynchronizationContext
   - Вызывает async-метод `FetchAvailableSeatsAsync()` и блокируется через `.Wait()` / `.Result`
   - `FetchAvailableSeatsAsync` делает `await Task.Delay(500)` — продолжение ждёт UI-поток
   - UI-поток ждёт `.Wait()` → DEADLOCK
   - Программа должна зависнуть (добавить таймаут с выводом «DEADLOCK DETECTED»)

4. **Сценарий 2: FIX через ConfigureAwait(false)** — метод `FixedWithConfigureAwait()`:
   - Тот же код, но `FetchAvailableSeatsAsync` использует `ConfigureAwait(false)` на ВСЕХ await
   - Продолжение на ThreadPool, а не в UI-потоке → deadlock не происходит
   - Демонстрирует: работает, но плохая практика для публичных API (библиотечный код не должен зависеть от ConfigureAwait)

5. **Сценарий 3: FIX через async all the way** — метод `FixedWithAsyncAllTheWay()`:
   - Не использовать `.Wait()` — метод `BookTicketAsync` полностью асинхронный
   - Показать, что это **правильное** решение: async от UI до БД и обратно

6. **Сценарий 4: FIX через Task.Run wrapper** — метод `FixedWithTaskRunWrapper()`:
   - Обернуть вызов в `Task.Run(() => FetchAvailableSeatsAsync()).GetAwaiter().GetResult()`
   - Задача уходит на ThreadPool (без UI-контекста) → deadlock не происходит
   - Подчеркнуть: это HACK для legacy-кода, не для нового

7. **Сценарий 5: DEADLOCK между двумя lock-ами** — метод `DeadlockWithTwoLocks()`:
   - Классический deadlock: два потока, два lock-а в разном порядке
   - Поток A: lock(lockA) → Thread.Sleep(50) → lock(lockB)
   - Поток B: lock(lockB) → Thread.Sleep(50) → lock(lockA)
   - Показать, что оба потока зависают навсегда
   - Исправить: всегда захватывать lock-и в одном порядке

### Ожидаемый вывод

```
=== SCENARIO 1: Deadlock with .Wait() ===
[UI Thread 5] Calling FetchAvailableSeatsAsync...
[UI Thread 5] .Wait() — blocked, waiting for task...
[ThreadPool] FetchAvailableSeatsAsync completed...
[ThreadPool] Trying to post continuation to UI thread...
[UI Thread 5] Still blocked in .Wait()...

<<< DEADLOCK DETECTED after 3000ms >>>
Cause: UI thread waits for Task. Task waits for UI thread.

=== SCENARIO 2: Fixed with ConfigureAwait(false) ===
[UI Thread 5] Calling FetchAvailableSeatsAsync...
[UI Thread 5] .Wait() — blocked...
[ThreadPool] Continuation on ThreadPool (ConfigureAwait(false))...
[UI Thread 5] .Wait() returned successfully!
Result: Seats available: 42

=== SCENARIO 3: Fixed with async all the way (BEST) ===
[UI Thread 5] Calling BookTicketAsync...
[UI Thread 5] Fetching seats...
[ThreadPool] Continuation (no context needed)...
[UI Thread 5] Booking confirmed!
Result: Booked seat A14

=== SCENARIO 4: Fixed with Task.Run wrapper (HACK) ===
[UI Thread 5] Wrapping in Task.Run...
[ThreadPool] FetchAvailableSeatsAsync on ThreadPool...
Result: Seats available: 42

=== SCENARIO 5: Two-lock deadlock ===
[Thread 7] Acquired lockA, waiting for lockB...
[Thread 8] Acquired lockB, waiting for lockA...
<<< TWO-LOCK DEADLOCK DETECTED >>>
FIX: Always acquire locks in the same order.

=== SCENARIO 5b: Fixed lock ordering ===
[Thread 9] Acquired lockA, then lockB — success!
[Thread 10] Acquired lockA, then lockB — success!
No deadlock.
```

### Ограничения
- Deadlock должен реально воспроизводиться (программа зависает)
- Каждый сценарий запускается изолированно
- Для кастомного SynchronizationContext — использовать `SingleThreadSynchronizationContext` (из задачи 2.2)
- Для обнаружения deadlock-а использовать таймаут

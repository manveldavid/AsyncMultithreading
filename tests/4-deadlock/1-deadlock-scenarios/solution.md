# Решение: Дебаггер deadlock-ов в системе бронирования билетов

```csharp
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

Console.WriteLine("=== SCENARIO 1: Deadlock with .Wait() ===\n");
await RunInUIContext(TicketBookingSystem.DeadlockWithWait);

Console.WriteLine("\n=== SCENARIO 2: Fixed with ConfigureAwait(false) ===\n");
await RunInUIContext(TicketBookingSystem.FixedWithConfigureAwait);

Console.WriteLine("\n=== SCENARIO 3: Fixed with async all the way (BEST) ===\n");
await RunInUIContext(TicketBookingSystem.FixedWithAsyncAllTheWay);

Console.WriteLine("\n=== SCENARIO 4: Fixed with Task.Run wrapper (HACK) ===\n");
await RunInUIContext(TicketBookingSystem.FixedWithTaskRunWrapper);

Console.WriteLine("\n=== SCENARIO 5: Two-lock deadlock ===\n");
TicketBookingSystem.DeadlockWithTwoLocks();

Console.WriteLine("\n=== SCENARIO 5b: Fixed lock ordering ===\n");
TicketBookingSystem.FixedLockOrdering();

static async Task RunInUIContext(Func<Task> action)
{
    using var ctx = new SingleThreadSynchronizationContext();
    SynchronizationContext.SetSynchronizationContext(ctx);

    await Task.Run(async () =>
    {
        SynchronizationContext.SetSynchronizationContext(ctx);
        try { await action(); }
        catch (Exception ex) { Console.WriteLine($"Exception: {ex.Message}"); }
    });

    SynchronizationContext.SetSynchronizationContext(null);
}

public static class TicketBookingSystem
{
    public static async Task DeadlockWithWait()
    {
        Console.WriteLine($"[UI Thread {Environment.CurrentManagedThreadId}] Calling FetchAvailableSeatsAsync...");

        var task = FetchAvailableSeatsAsync();
        Console.WriteLine($"[UI Thread {Environment.CurrentManagedThreadId}] .Wait() — blocked...");

        var completedTask = Task.WhenAny(task, Task.Delay(3000)).Result;

        if (completedTask == task)
        {
            Console.WriteLine("Task completed (no deadlock — unexpected)");
        }
        else
        {
            Console.WriteLine($"\n<<< DEADLOCK DETECTED after 3000ms >>>");
            Console.WriteLine("Cause: UI thread waits for Task. Task waits for UI thread.");
        }
    }

    public static async Task FixedWithConfigureAwait()
    {
        Console.WriteLine($"[UI Thread {Environment.CurrentManagedThreadId}] Calling FetchAvailableSeatsWithCA...");

        var task = FetchAvailableSeatsWithCA();
        Console.WriteLine($"[UI Thread {Environment.CurrentManagedThreadId}] .Wait() — blocked...");

        task.Wait();
        Console.WriteLine($"[UI Thread {Environment.CurrentManagedThreadId}] .Wait() returned successfully!");
        Console.WriteLine($"Result: {task.Result}");
    }

    public static async Task FixedWithAsyncAllTheWay()
    {
        Console.WriteLine($"[UI Thread {Environment.CurrentManagedThreadId}] Calling BookTicketAsync (fully async)...");
        string result = await BookTicketAsync();
        Console.WriteLine($"Result: {result}");
    }

    public static async Task FixedWithTaskRunWrapper()
    {
        Console.WriteLine($"[UI Thread {Environment.CurrentManagedThreadId}] Wrapping in Task.Run...");
        string result = Task.Run(() => FetchAvailableSeatsWithCA()).GetAwaiter().GetResult();
        Console.WriteLine($"Result: {result}");
        Console.WriteLine("NOTE: This is a HACK for legacy code. Use async all the way in new code.");
    }

    public static void DeadlockWithTwoLocks()
    {
        var lockA = new object();
        var lockB = new object();
        var completed = 0;

        Task.Run(() =>
        {
            lock (lockA)
            {
                int tid = Environment.CurrentManagedThreadId;
                Console.WriteLine($"[Thread {tid}] Acquired lockA, waiting for lockB...");
                Thread.Sleep(100);
                lock (lockB)
                {
                    Interlocked.Increment(ref completed);
                }
            }
        });

        Task.Run(() =>
        {
            lock (lockB)
            {
                int tid = Environment.CurrentManagedThreadId;
                Console.WriteLine($"[Thread {tid}] Acquired lockB, waiting for lockA...");
                Thread.Sleep(100);
                lock (lockA)
                {
                    Interlocked.Increment(ref completed);
                }
            }
        });

        Thread.Sleep(3000);
        if (completed < 2)
            Console.WriteLine("<<< TWO-LOCK DEADLOCK DETECTED >>>");
        Console.WriteLine("FIX: Always acquire locks in the same order.");
    }

    public static void FixedLockOrdering()
    {
        var lockA = new object();
        var lockB = new object();
        var completed = 0;

        Task.Run(() =>
        {
            lock (lockA)
            {
                int tid = Environment.CurrentManagedThreadId;
                Console.WriteLine($"[Thread {tid}] Acquired lockA, then lockB — success!");
                Thread.Sleep(50);
                lock (lockB) { Interlocked.Increment(ref completed); }
            }
        });

        Task.Run(() =>
        {
            lock (lockA) // Same order as thread 1!
            {
                int tid = Environment.CurrentManagedThreadId;
                Console.WriteLine($"[Thread {tid}] Acquired lockA, then lockB — success!");
                Thread.Sleep(50);
                lock (lockB) { Interlocked.Increment(ref completed); }
            }
        });

        Thread.Sleep(2000);
        Console.WriteLine(completed == 2 ? "No deadlock — lock ordering consistent." : "Unexpected deadlock.");
    }

    private static async Task<string> FetchAvailableSeatsAsync()
    {
        Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] Fetching seats from DB...");
        await Task.Delay(500);
        Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] DB returned, posting to UI...");
        return "Seats available: 42";
    }

    private static async Task<string> FetchAvailableSeatsWithCA()
    {
        Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] Fetching seats from DB...");
        await Task.Delay(200).ConfigureAwait(false);
        Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] DB returned (ThreadPool)...");
        return "Seats available: 42";
    }

    private static async Task<string> BookTicketAsync()
    {
        Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] Fetching seats...");
        await Task.Delay(200).ConfigureAwait(false);
        Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] Selecting seat...");
        await Task.Delay(100).ConfigureAwait(false);
        return "Booked seat A14";
    }
}

public class SingleThreadSynchronizationContext : SynchronizationContext
{
    private readonly BlockingCollection<(SendOrPostCallback, object?)> _queue = new();
    private readonly Thread _worker;
    private volatile bool _running = true;
    public int WorkerThreadId => _worker.ManagedThreadId;

    public SingleThreadSynchronizationContext()
    {
        _worker = new Thread(RunLoop) { IsBackground = true, Name = "UI-Context" };
        _worker.Start();
    }

    private void RunLoop()
    {
        SynchronizationContext.SetSynchronizationContext(this);
        while (_running)
        {
            if (_queue.TryTake(out var item, 50))
            {
                try { item.Item1(item.Item2); }
                catch (Exception ex) { Console.WriteLine($"[UI Context] Unhandled: {ex.Message}"); }
            }
        }
    }

    public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));
    public override void Send(SendOrPostCallback d, object? state)
    {
        using var mre = new ManualResetEventSlim();
        _queue.Add((s => { try { d(s); } finally { mre.Set(); } }, state));
        mre.Wait();
    }

    public void StopAndWait() { _running = false; _worker.Join(2000); }
}
```

## Ключевые моменты

1. **Механизм deadlock с .Wait()**: UI-поток вызывает `.Wait()` и блокируется. Task после `await` пытается вернуть continuation в UI-поток через `SynchronizationContext.Post()`. Но UI-поток занят в `.Wait()`. Круговая зависимость.

2. **ConfigureAwait(false)**: говорит «не захватывай SynchronizationContext для продолжения». Продолжение уходит на ThreadPool, UI-поток освобождается. Правило для библиотек — всегда.

3. **async all the way**: правильный подход — не блокировать async-код. Весь путь от UI до БД делается через `await`.

4. **Task.Run wrapper**: hack для legacy-кода. Запускает async-метод на ThreadPool (где нет UI-контекста), поэтому deadlock не возникает. Но это не решение, а костыль.

5. **Two-lock deadlock**: если два потока захватывают два lock-а в разном порядке — deadlock. Исправление: всегда один порядок захвата.

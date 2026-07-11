# Решение: Счётчик посещений с lock-free алгоритмами

```csharp
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

Console.WriteLine("=== Non-Atomic Counter ===\n");
VisitCounter.DemonstrateNonAtomicCounter();

Console.WriteLine("\n=== Interlocked.Increment ===\n");
VisitCounter.InterlockedIncrementCounter();

Console.WriteLine("\n=== Interlocked.CompareExchange (CAS) — Top Views ===\n");
VisitCounter.InterlockedCompareExchangeMax();

Console.WriteLine("\n=== Interlocked.Exchange — Worker Swap ===\n");
VisitCounter.InterlockedExchangeExample();

Console.WriteLine("\n=== Volatile Flag ===\n");
VisitCounter.VolatileFlagDemo();

Console.WriteLine("\n=== Volatile.Read/Write ===\n");
VisitCounter.VolatileReadWriteExample();

public static class VisitCounter
{
    public static void DemonstrateNonAtomicCounter()
    {
        int counter = 0;
        int threads = 10;
        int iterations = 100_000;
        long expected = threads * iterations;

        var tasks = new Task[threads];
        for (int t = 0; t < threads; t++)
            tasks[t] = Task.Run(() => { for (int i = 0; i < iterations; i++) counter++; });

        Task.WhenAll(tasks).Wait();
        long lost = expected - counter;
        Console.WriteLine($"Expected: {expected:N0}");
        Console.WriteLine($"Actual:   {counter:N0}");
        Console.WriteLine($"Lost: {lost:N0} ({lost * 100.0 / expected:F1}%)");
    }

    public static void InterlockedIncrementCounter()
    {
        int counter = 0;
        int threads = 10;
        int iterations = 100_000;
        long expected = threads * iterations;

        var sw = Stopwatch.StartNew();
        var tasks = new Task[threads];
        for (int t = 0; t < threads; t++)
            tasks[t] = Task.Run(() => { for (int i = 0; i < iterations; i++) Interlocked.Increment(ref counter); });

        Task.WhenAll(tasks).Wait();
        sw.Stop();

        Console.WriteLine($"Expected: {expected:N0}");
        Console.WriteLine($"Actual:   {counter:N0}");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
    }

    public static void InterlockedCompareExchangeMax()
    {
        int topViews = 0;
        var rng = new Random();

        var tasks = new Task[100];
        for (int t = 0; t < tasks.Length; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    int newViews = rng.Next(1, 1000);

                    int original;
                    do
                    {
                        original = topViews;
                        if (newViews <= original) break; // not higher, skip
                    }
                    while (Interlocked.CompareExchange(ref topViews, newViews, original) != original);

                    if (newViews > original)
                        Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] New high score: {newViews} views (old: {original})");
                }
            });
        }

        Task.WhenAll(tasks).Wait();
        Console.WriteLine($"\nFinal top views: {topViews} (correct maximum!)");
    }

    public static void InterlockedExchangeExample()
    {
        string activeWorker = "Worker-A";
        Console.WriteLine($"Old worker: {activeWorker}");

        string oldWorker = Interlocked.Exchange(ref activeWorker, "Worker-B");
        Console.WriteLine($"Swapped to: {activeWorker}");
        Console.WriteLine($"Old worker disposed: {oldWorker}");
    }

    public static void VolatileFlagDemo()
    {
        var flag = new VolatileFlag();
        var worker = Task.Run(() => flag.WorkerLoop());
        Thread.Sleep(200);

        Console.WriteLine("[Main] Setting stop flag...");
        flag.Stop();
        worker.Wait();

        Console.WriteLine("\nWithout volatile — worker might run forever due to CPU caching/reordering.");
        Console.WriteLine("volatile guarantees: write happens-before read across threads.");
    }

    public static void VolatileReadWriteExample()
    {
        bool flag = false;

        var worker = Task.Run(() =>
        {
            Console.WriteLine("[Worker] Started with Volatile.Read...");
            while (!Volatile.Read(ref flag))
            {
                Thread.Sleep(10);
            }
            Console.WriteLine("[Worker] Volatile.Read saw flag change!");
        });

        Thread.Sleep(100);
        Console.WriteLine("[Main] Setting flag via Volatile.Write...");
        Volatile.Write(ref flag, true);
        worker.Wait();

        Console.WriteLine("Volatile.Read/Write: same as volatile keyword, but works for any variable.");
    }
}

public class VolatileFlag
{
    private volatile bool _shouldStop;

    public void WorkerLoop()
    {
        Console.WriteLine("[Worker] Started, waiting for stop signal...");
        while (!_shouldStop) { Thread.Sleep(10); }
        Console.WriteLine("[Worker] Stop signal received! Shutting down.");
    }

    public void Stop() => _shouldStop = true;
}
```

## Ключевые моменты

1. **volatile**: запрещает компилятору и CPU переупорядочивать операции с переменной. Гарантирует: все операции ДО volatile-записи видны ДО volatile-чтения на другом потоке. **Не гарантирует атомарность** — `x++` всё ещё не атомарно.

2. **Interlocked.Increment**: атомарный инкремент через CPU-инструкцию `LOCK XADD`. Lock-free, быстрее чем `lock`.

3. **Interlocked.CompareExchange (CAS)**: атомарно сравнивает и заменяет. CAS-цикл `do { ... } while (CompareExchange(...) != original)` — основа lock-free алгоритмов.

4. **Interlocked.Exchange**: атомарно заменяет значение и возвращает старое. Используется для swap-операций.

5. **Volatile.Read/Write**: явные барьеры памяти — эквивалент volatile для любых переменных. Полезно когда поле не может быть volatile (например, элемент массива).

6. **Когда что использовать**:
   - `volatile` — простые флаги (`_shouldStop`)
   - `Interlocked` — атомарные операции (increment, CAS, swap)
   - `lock` — сложные секции из нескольких операций

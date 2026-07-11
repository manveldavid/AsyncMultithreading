# Решение: Кэш товаров — Dictionary vs ConcurrentDictionary

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

Console.WriteLine("=== Dictionary Race Condition ===\n");
ProductCatalog.DemonstrateDictionaryRaceCondition();

Console.WriteLine("\n=== Dictionary + lock ===\n");
ProductCatalog.FixWithLockedDictionary();

Console.WriteLine("\n=== ConcurrentDictionary ===\n");
ProductCatalog.FixWithConcurrentDictionary();

Console.WriteLine("\n=== GetOrAdd Double-Call Caveat ===\n");
ProductCatalog.DemonstrateGetOrAddDoubleCall();

Console.WriteLine("\n=== AddOrUpdate — Atomic Price Update ===\n");
ProductCatalog.DemonstrateAddOrUpdate();

Console.WriteLine("\n=== Benchmark: Read-heavy vs Write-heavy ===\n");
ProductCatalog.BenchmarkReadVsWrite();

public record Product(int Id, string Name, decimal Price);

public static class ProductCatalog
{
    public static void DemonstrateDictionaryRaceCondition()
    {
        var dict = new Dictionary<int, Product>();
        int threads = 10;
        int itemsPerThread = 10_000;
        bool hadException = false;

        var tasks = new Task[threads];
        for (int t = 0; t < threads; t++)
        {
            int baseId = t * itemsPerThread;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < itemsPerThread; i++)
                {
                    try
                    {
                        dict[baseId + i] = new Product(baseId + i, $"Product-{baseId + i}", i * 10m);
                    }
                    catch (Exception ex)
                    {
                        if (!hadException)
                        {
                            hadException = true;
                            Console.WriteLine($"Exception: {ex.GetType().Name}: {ex.Message}");
                        }
                    }
                }
            });
        }

        Task.WhenAll(tasks).Wait();

        Console.WriteLine($"Dictionary Count: {dict.Count:N0} (expected {threads * itemsPerThread:N0})");
        if (!hadException && dict.Count < threads * itemsPerThread)
            Console.WriteLine($"Lost: {threads * itemsPerThread - dict.Count:N0} entries. OR ArgumentException thrown!");
    }

    public static void FixWithLockedDictionary()
    {
        var dict = new Dictionary<int, Product>();
        var lockObj = new object();
        int threads = 10;
        int itemsPerThread = 10_000;

        var sw = Stopwatch.StartNew();
        var tasks = new Task[threads];
        for (int t = 0; t < threads; t++)
        {
            int baseId = t * itemsPerThread;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < itemsPerThread; i++)
                {
                    lock (lockObj) { dict[baseId + i] = new Product(baseId + i, $"P-{baseId + i}", i * 10m); }
                }
            });
        }
        Task.WhenAll(tasks).Wait();
        sw.Stop();

        Console.WriteLine($"Count: {dict.Count:N0}");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
    }

    public static void FixWithConcurrentDictionary()
    {
        var cd = new ConcurrentDictionary<int, Product>();
        int threads = 10;
        int itemsPerThread = 10_000;

        var sw = Stopwatch.StartNew();
        var tasks = new Task[threads];
        for (int t = 0; t < threads; t++)
        {
            int baseId = t * itemsPerThread;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < itemsPerThread; i++)
                {
                    int id = baseId + i;
                    cd.TryAdd(id, new Product(id, $"P-{id}", i * 10m));
                }
            });
        }
        Task.WhenAll(tasks).Wait();
        sw.Stop();

        Console.WriteLine($"Count: {cd.Count:N0}");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms (faster due to lock striping)");
    }

    public static void DemonstrateGetOrAddDoubleCall()
    {
        var cd = new ConcurrentDictionary<string, string>();
        int factoryCalls = 0;

        var tasks = new Task[10];
        for (int t = 0; t < 10; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                cd.GetOrAdd("key", k =>
                {
                    Interlocked.Increment(ref factoryCalls);
                    Thread.Sleep(20);
                    return "expensive-result";
                });
            });
        }
        Task.WhenAll(tasks).Wait();

        Console.WriteLine($"Factory called {factoryCalls} times! (expected 1)");
        Console.WriteLine("WARNING: GetOrAdd valueFactory may run multiple times under contention!");

        // Fix with Lazy<T>
        var cd2 = new ConcurrentDictionary<string, Lazy<string>>();
        int factoryCalls2 = 0;

        var tasks2 = new Task[10];
        for (int t = 0; t < 10; t++)
        {
            tasks2[t] = Task.Run(() =>
            {
                var lazy = cd2.GetOrAdd("key", k => new Lazy<string>(() =>
                {
                    Interlocked.Increment(ref factoryCalls2);
                    Thread.Sleep(20);
                    return "expensive-result";
                }));
                _ = lazy.Value;
            });
        }
        Task.WhenAll(tasks2).Wait();

        Console.WriteLine($"\nFix with Lazy<T>: Factory called {factoryCalls2} time(s). (correct!)");
    }

    public static void DemonstrateAddOrUpdate()
    {
        var cd = new ConcurrentDictionary<int, Product>();
        cd.TryAdd(42, new Product(42, "Widget", 100m));
        Console.WriteLine($"Product #42: initial price = ${cd[42].Price}");

        var tasks = new Task[3];
        var prices = new[] { 150m, 120m, 180m };
        for (int t = 0; t < 3; t++)
        {
            decimal proposed = prices[t];
            tasks[t] = Task.Run(() =>
            {
                cd.AddOrUpdate(42,
                    _ => new Product(42, "Widget", proposed),
                    (_, existing) =>
                    {
                        if (proposed > existing.Price)
                        {
                            Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] Proposed ${proposed} → Accepted");
                            return new Product(42, "Widget", proposed);
                        }
                        Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] Proposed ${proposed} → Rejected (current ${existing.Price} > ${proposed})");
                        return existing;
                    });
            });
        }
        Task.WhenAll(tasks).Wait();

        Console.WriteLine($"Final price: ${cd[42].Price}");
    }

    public static void BenchmarkReadVsWrite()
    {
        const int operations = 100_000;
        var rng = new Random(42);
        var data = new List<int>(operations);
        for (int i = 0; i < operations; i++) data.Add(rng.Next(1000));

        foreach (string scenario in new[] { "90% reads", "10% reads" })
        {
            int readChance = scenario == "90% reads" ? 90 : 10;
            var preloadedDict = new Dictionary<int, int>();
            var preloadedCd = new ConcurrentDictionary<int, int>();
            for (int i = 0; i < 1000; i++) { preloadedDict[i] = i; preloadedCd[i] = i; }

            var lockObj = new object();
            var swDict = Stopwatch.StartNew();
            Parallel.For(0, operations, i =>
            {
                if (rng.Next(100) < readChance)
                {
                    int key = data[i];
                    lock (lockObj) { preloadedDict.TryGetValue(key, out _); }
                }
                else
                {
                    int key = data[i];
                    lock (lockObj) { preloadedDict[key] = key; }
                }
            });
            swDict.Stop();

            var swCd = Stopwatch.StartNew();
            Parallel.For(0, operations, i =>
            {
                if (rng.Next(100) < readChance)
                {
                    preloadedCd.TryGetValue(data[i], out _);
                }
                else
                {
                    preloadedCd[data[i]] = data[i];
                }
            });
            swCd.Stop();

            string rec = scenario == "90% reads"
                ? "lock+Dict OK for reads, CD has overhead"
                : "ConcurrentDictionary wins (lock striping)";

            Console.WriteLine($"{scenario}: lock+Dict={swDict.ElapsedMilliseconds}ms | ConcurrentDict={swCd.ElapsedMilliseconds}ms → {rec}");
        }
    }
}
```

## Ключевые моменты

1. **Dictionary не thread-safe**: при параллельном добавлении данные теряются, возможны исключения и infinite loop (в .NET Framework).

2. **ConcurrentDictionary**: lock striping (сегментированные lock-и) — разные потоки работают с разными сегментами. Выше throughput при записи.

3. **GetOrAdd caveat**: valueFactory может вызваться несколько раз, если несколько потоков одновременно видят отсутствие ключа. Исправление: `ConcurrentDictionary + Lazy<T>`.

4. **AddOrUpdate**: атомарная read-modify-write. updateValueFactory может вызываться несколько раз (аналогично GetOrAdd), но финальное значение выбирается атомарно.

5. **Read-heavy vs Write-heavy**: для read-heavy `lock + Dictionary` может быть быстрее (меньше overhead). Для write-heavy — `ConcurrentDictionary` (lock striping). Для компромисса — `ConcurrentDictionary`.

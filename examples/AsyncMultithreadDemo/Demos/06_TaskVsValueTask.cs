using System.Diagnostics;

namespace AsyncMultithreadDemo.Demos;

public static class Demo06_TaskVsValueTask
{
    public static async Task Run()
    {
        Console.WriteLine("=== Task vs ValueTask ===\n");

        Console.WriteLine("  Task<T>:   class, heap allocation, GC pressure");
        Console.WriteLine("  ValueTask<T>: struct, stack allocation (when synchronous)");
        Console.WriteLine("  ValueTask is ideal when method completes synchronously >90% of time.\n");

        Console.WriteLine("--- 1. Benchmark: 1,000,000 synchronous calls ---\n");

        long memBeforeTask = GC.GetTotalMemory(forceFullCollection: true);
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 1_000_000; i++)
        {
            await GetFromCacheTaskAsync(i);
        }

        sw.Stop();
        long memAfterTask = GC.GetTotalMemory(forceFullCollection: false);
        long taskTime = sw.ElapsedMilliseconds;
        long taskAlloc = memAfterTask - memBeforeTask;

        Console.WriteLine($"  Task<int>:     {taskTime}ms, allocations: {taskAlloc / 1024.0 / 1024.0:F1} MB");

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long memBeforeValue = GC.GetTotalMemory(forceFullCollection: true);
        sw.Restart();

        for (int i = 0; i < 1_000_000; i++)
        {
            await GetFromCacheValueTaskAsync(i);
        }

        sw.Stop();
        long memAfterValue = GC.GetTotalMemory(forceFullCollection: false);
        long valueTime = sw.ElapsedMilliseconds;
        long valueAlloc = memAfterValue - memBeforeValue;

        Console.WriteLine($"  ValueTask<int>: {valueTime}ms, allocations: {valueAlloc / 1024.0 / 1024.0:F1} MB");

        Console.WriteLine($"\n  Speedup: {(double)taskTime / Math.Max(valueTime, 1):F1}x");
        Console.WriteLine($"  Memory saved: {(taskAlloc - valueAlloc) / 1024.0 / 1024.0:F1} MB\n");

        Console.WriteLine("--- 2. ValueTask limitations ---\n");

        Console.WriteLine("  ValueTask CANNOT be awaited multiple times:");
        var vt = GetFromCacheValueTaskAsync(42);
        int result1 = await vt;
        Console.WriteLine($"    First await: {result1}");

        Console.WriteLine("    Second await would throw InvalidOperationException");

        Console.WriteLine("\n  ValueTask CANNOT be used with Task.WhenAll:");
        Console.WriteLine("    Task.WhenAll requires Task objects");
        Console.WriteLine("    Workaround: convert to Task via .AsTask() (allocates!)");

        Console.WriteLine("\n--- 3. When to use ValueTask ---");
        Console.WriteLine("  ✓ Hot path, millions of calls");
        Console.WriteLine("  ✓ Method completes synchronously most of the time (cache, etc.)");
        Console.WriteLine("  ✓ IValueTaskSource for pooling (Socket, FileStream)");
        Console.WriteLine("  ✗ NOT for: WhenAll, multiple awaits, complex composition");
        Console.WriteLine("  ✗ NOT for: methods that always go async (DB calls, HTTP)");
    }

    private static readonly Dictionary<int, int> _cache = Enumerable
        .Range(0, 100)
        .ToDictionary(x => x, x => x * x);

    private static Task<int> GetFromCacheTaskAsync(int key)
    {
        if (_cache.TryGetValue(key, out int value))
            return Task.FromResult(value);
        return Task.Run(() => key * key);
    }

    private static ValueTask<int> GetFromCacheValueTaskAsync(int key)
    {
        if (_cache.TryGetValue(key, out int value))
            return new ValueTask<int>(value);
        return new ValueTask<int>(Task.Run(() => key * key));
    }
}

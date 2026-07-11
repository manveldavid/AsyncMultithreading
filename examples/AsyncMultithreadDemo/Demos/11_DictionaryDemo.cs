using System.Collections.Concurrent;
using System.Diagnostics;

namespace AsyncMultithreadDemo.Demos;

public static class Demo11_DictionaryDemo
{
    public static async Task Run()
    {
        Console.WriteLine("=== Dictionary vs ConcurrentDictionary ===\n");

        Console.WriteLine("--- 1. Dictionary: race condition ---\n");

        var dict = new Dictionary<int, int>();
        int errors = 0;
        int iterations = 100_000;

        var tasks = Enumerable.Range(0, 10).Select(id => Task.Run(() =>
        {
            for (int i = 0; i < iterations / 10; i++)
            {
                try
                {
                    dict[i] = i;
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref errors);
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        Console.WriteLine($"  Dictionary after 10 threads writing {iterations / 10} items each:");
        Console.WriteLine($"    Count: {dict.Count} (expected {iterations / 10})");
        Console.WriteLine($"    Exceptions caught: {errors}");
        Console.WriteLine("    Data loss and exceptions are possible!\n");

        Console.WriteLine("--- 2. ConcurrentDictionary: thread-safe ---\n");

        var cdict = new ConcurrentDictionary<int, int>();
        errors = 0;

        tasks = Enumerable.Range(0, 10).Select(id => Task.Run(() =>
        {
            for (int i = 0; i < iterations / 10; i++)
            {
                try
                {
                    cdict[i] = i;
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref errors);
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        Console.WriteLine($"  ConcurrentDictionary after 10 threads:");
        Console.WriteLine($"    Count: {cdict.Count} (expected {iterations / 10})");
        Console.WriteLine($"    Exceptions: {errors}\n");

        Console.WriteLine("--- 3. ConcurrentDictionary API ---\n");

        var cd = new ConcurrentDictionary<string, int>();

        cd.TryAdd("alpha", 1);
        cd.TryAdd("beta", 2);
        Console.WriteLine($"  TryAdd: alpha={cd["alpha"]}, beta={cd["beta"]}");

        bool added = cd.TryAdd("alpha", 999);
        Console.WriteLine($"  TryAdd duplicate: {added} (value unchanged: {cd["alpha"]})");

        int updated = cd.AddOrUpdate("alpha", 0, (key, old) => old + 100);
        Console.WriteLine($"  AddOrUpdate: alpha={updated}");

        int val = cd.GetOrAdd("gamma", key => key.Length);
        Console.WriteLine($"  GetOrAdd 'gamma': {val}");

        Console.WriteLine("\n  CAVEAT: GetOrAdd valueFactory may be called MULTIPLE times!");
        Console.WriteLine("  If thread-safety of the value matters, use AddOrUpdate or pre-compute.\n");

        int factoryCalls = 0;
        var cd2 = new ConcurrentDictionary<string, string>();

        var factoryTasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            cd2.GetOrAdd("key", k =>
            {
                Interlocked.Increment(ref factoryCalls);
                Thread.Sleep(10);
                return "value";
            });
        })).ToArray();

        await Task.WhenAll(factoryTasks);
        Console.WriteLine($"  GetOrAdd factory called {factoryCalls} times for 10 concurrent calls (expected 1, actual may be >1)");

        Console.WriteLine("\n--- 4. Benchmark: lock+Dictionary vs ConcurrentDictionary ---\n");

        var plainDict = new Dictionary<int, int>();
        var lockDict = new Dictionary<int, int>();
        var concDict = new ConcurrentDictionary<int, int>();
        int benchIterations = 500_000;

        var sw = Stopwatch.StartNew();
        var benchTasks = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < benchIterations / 4; i++)
                plainDict[i] = i;
        })).ToArray();
        await Task.WhenAll(benchTasks);
        sw.Stop();
        Console.WriteLine($"  Dictionary (unsafe, parallel): {sw.ElapsedMilliseconds}ms, count={plainDict.Count}");

        sw.Restart();
        var lockObj = new object();
        benchTasks = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < benchIterations / 4; i++)
                lock (lockDict)
                    lockDict[i] = i;
        })).ToArray();
        await Task.WhenAll(benchTasks);
        sw.Stop();
        Console.WriteLine($"  lock + Dictionary: {sw.ElapsedMilliseconds}ms, count={lockDict.Count}");

        sw.Restart();
        benchTasks = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < benchIterations / 4; i++)
                concDict[i] = i;
        })).ToArray();
        await Task.WhenAll(benchTasks);
        sw.Stop();
        Console.WriteLine($"  ConcurrentDictionary: {sw.ElapsedMilliseconds}ms, count={concDict.Count}");

        Console.WriteLine("\n--- 5. When to use what ---\n");
        Console.WriteLine("  Dictionary + lock:");
        Console.WriteLine("    ✓ Read-heavy workloads (lock only on writes)");
        Console.WriteLine("    ✓ Complex multi-step operations");
        Console.WriteLine();
        Console.WriteLine("  ConcurrentDictionary:");
        Console.WriteLine("    ✓ Write-heavy, high contention");
        Console.WriteLine("    ✓ Fine-grained locking (lock striping)");
        Console.WriteLine("    ✓ Simple atomic operations (TryAdd, GetOrAdd)");
        Console.WriteLine();
        Console.WriteLine("  ImmutableDictionary + Interlocked.Swap:");
        Console.WriteLine("    ✓ Read-dominated, rare writes");
        Console.WriteLine("    ✓ Lock-free reads");
    }
}

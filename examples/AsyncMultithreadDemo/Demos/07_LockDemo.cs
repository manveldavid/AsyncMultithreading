using System.Diagnostics;

namespace AsyncMultithreadDemo.Demos;

public static class Demo07_LockDemo
{
    public static async Task Run()
    {
        Console.WriteLine("=== lock, Monitor, Interlocked ===\n");

        Console.WriteLine("--- 1. Race condition: counter++ from 10 threads ---\n");

        int counter = 0;
        int iterations = 100_000;
        var threads = new Thread[10];

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(() =>
            {
                for (int j = 0; j < iterations / threads.Length; j++)
                    counter++;
            });
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        sw.Stop();
        Console.WriteLine($"  Without lock: counter={counter}, expected={iterations}");
        Console.WriteLine($"  Lost updates: {iterations - counter} ({(iterations - counter) * 100.0 / iterations:F1}%)");
        Console.WriteLine($"  Time: {sw.ElapsedMilliseconds}ms\n");

        Console.WriteLine("--- 2. Fix with lock ---\n");

        counter = 0;
        var lockObj = new object();

        sw.Restart();
        threads = new Thread[10];
        for (int i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(() =>
            {
                for (int j = 0; j < iterations / threads.Length; j++)
                    lock (lockObj)
                        counter++;
            });
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        sw.Stop();
        Console.WriteLine($"  With lock: counter={counter}, expected={iterations}");
        Console.WriteLine($"  Lost updates: {iterations - counter}");
        Console.WriteLine($"  Time: {sw.ElapsedMilliseconds}ms\n");

        Console.WriteLine("--- 3. Fix with Interlocked (lock-free) ---\n");

        counter = 0;

        sw.Restart();
        threads = new Thread[10];
        for (int i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(() =>
            {
                for (int j = 0; j < iterations / threads.Length; j++)
                    Interlocked.Increment(ref counter);
            });
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        sw.Stop();
        Console.WriteLine($"  With Interlocked: counter={counter}, expected={iterations}");
        Console.WriteLine($"  Lost updates: {iterations - counter}");
        Console.WriteLine($"  Time: {sw.ElapsedMilliseconds}ms\n");

        Console.WriteLine("--- 4. Monitor: TryEnter with timeout ---\n");

        var monitorObj = new object();
        bool acquired = Monitor.TryEnter(monitorObj, TimeSpan.FromMilliseconds(100));
        Console.WriteLine($"  TryEnter(100ms): acquired={acquired}");

        if (acquired)
        {
            try
            {
                Console.WriteLine("  Inside critical section");
            }
            finally
            {
                Monitor.Exit(monitorObj);
            }
        }

        Console.WriteLine("\n--- 5. Named Mutex (single-instance app) ---\n");
        Console.WriteLine("  Run MutexApp.Second while this is running to see mutex in action.");

        using var mutex = new Mutex(true, "Global\\AsyncMultithreadDemoMutex", out bool createdNew);
        Console.WriteLine($"  Mutex created: {createdNew}");
        Console.WriteLine("  Holding mutex for 30 seconds...");
        Console.WriteLine("  Run MutexApp.Second now to see it detect the existing instance.\n");

        await Task.Delay(30_000);
        Console.WriteLine("  Mutex released.");
    }
}

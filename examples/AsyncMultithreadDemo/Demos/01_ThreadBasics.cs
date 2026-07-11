using System.Collections.Concurrent;
using System.Diagnostics;

namespace AsyncMultithreadDemo.Demos;

public static class Demo01_ThreadBasics
{
    public static async Task Run()
    {
        Console.WriteLine("=== 1. Thread Creation, Join, IsBackground ===\n");

        Console.WriteLine("[Foreground thread — prevents process exit]");
        var foreground = new Thread(() =>
        {
            Thread.Sleep(2000);
            Console.WriteLine($"  Foreground thread done. ManagedThreadId={Environment.CurrentManagedThreadId}");
        })
        { IsBackground = false };

        Console.WriteLine("[Background thread — does NOT prevent process exit]");
        var background = new Thread(() =>
        {
            Thread.Sleep(500);
            Console.WriteLine($"  Background thread done. ManagedThreadId={Environment.CurrentManagedThreadId}");
        })
        { IsBackground = true };

        foreground.Start();
        background.Start();
        foreground.Join();

        Console.WriteLine($"\n  ProcessorCount: {Environment.ProcessorCount}");

        Console.WriteLine("\n=== 2. ThreadPool Stats ===\n");
        ThreadPool.GetMinThreads(out int minWorker, out int minIo);
        ThreadPool.GetMaxThreads(out int maxWorker, out int maxIo);
        ThreadPool.GetAvailableThreads(out int availWorker, out int availIo);

        Console.WriteLine($"  Min worker threads: {minWorker}");
        Console.WriteLine($"  Max worker threads: {maxWorker}");
        Console.WriteLine($"  Available worker threads: {availWorker}");
        Console.WriteLine($"  In use: {maxWorker - availWorker}");
        Console.WriteLine($"  Min IO threads: {minIo}");
        Console.WriteLine($"  Max IO threads: {maxIo}");

        Console.WriteLine("\n=== 3. ThreadPool.QueueUserWorkItem ===\n");

        var sw = Stopwatch.StartNew();
        var cde = new CountdownEvent(10);

        for (int i = 0; i < 10; i++)
        {
            int captured = i;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(100);
                Console.WriteLine($"  Pool thread {captured}: id={Environment.CurrentManagedThreadId}");
                cde.Signal();
            });
        }

        cde.Wait();
        sw.Stop();
        Console.WriteLine($"  10 x 100ms tasks completed in {sw.ElapsedMilliseconds}ms (parallel)\n");

        Console.WriteLine("=== 4. Parallel.For vs Sequential ===\n");

        int[] data = Enumerable.Range(0, 100_000_000).ToArray();
        long sequentialSum = 0;

        sw.Restart();
        for (int i = 0; i < data.Length; i++)
            sequentialSum += data[i];
        sw.Stop();
        long seqTime = sw.ElapsedMilliseconds;

        long parallelSum = 0;
        sw.Restart();
        Parallel.For(0, data.Length,
            () => 0L,
            (i, _, local) => local + data[i],
            local => Interlocked.Add(ref parallelSum, local));
        sw.Stop();
        long parTime = sw.ElapsedMilliseconds;

        Console.WriteLine($"  Sequential sum={sequentialSum}, time={seqTime}ms");
        Console.WriteLine($"  Parallel   sum={parallelSum}, time={parTime}ms");
        Console.WriteLine($"  Speedup: {(double)seqTime / Math.Max(parTime, 1):F1}x");
    }
}

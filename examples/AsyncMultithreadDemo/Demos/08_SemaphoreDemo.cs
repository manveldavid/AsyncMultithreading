using System.Collections.Concurrent;
using System.Diagnostics;

namespace AsyncMultithreadDemo.Demos;

public static class Demo08_SemaphoreDemo
{
    public static async Task Run()
    {
        Console.WriteLine("=== Semaphore / SemaphoreSlim — HTTP throttling ===\n");

        const string url = "http://89.22.229.58:8080";
        const int totalRequests = 20;
        const int maxConcurrency = 3;

        Console.WriteLine($"  Target: {url}");
        Console.WriteLine($"  Total requests: {totalRequests}");
        Console.WriteLine($"  Max concurrent: {maxConcurrency}\n");

        Console.WriteLine("--- 1. Without throttling (all 20 at once) ---\n");

        var sw = Stopwatch.StartNew();
        var results1 = await SendRequestsAsync(totalRequests, url, throttle: false, maxConcurrency: 0);
        sw.Stop();

        PrintResults(results1, sw.ElapsedMilliseconds);

        Console.WriteLine("--- 2. With SemaphoreSlim(3) ---\n");

        sw.Restart();
        var results2 = await SendRequestsAsync(totalRequests, url, throttle: true, maxConcurrency);
        sw.Stop();

        PrintResults(results2, sw.ElapsedMilliseconds);

        Console.WriteLine("--- 3. Semaphore (kernel-level) vs SemaphoreSlim ---\n");
        Console.WriteLine("  SemaphoreSlim:");
        Console.WriteLine("    ✓ Async-friendly: WaitAsync()");
        Console.WriteLine("    ✓ Lightweight, user-mode");
        Console.WriteLine("    ✓ CancellationToken support");
        Console.WriteLine("    ✗ Single process only");
        Console.WriteLine();
        Console.WriteLine("  Semaphore (kernel):");
        Console.WriteLine("    ✓ Named — works across processes");
        Console.WriteLine("    ✗ No async Wait()");
        Console.WriteLine("    ✗ Slower (kernel transition)");
        Console.WriteLine("    ✓ Use for: cross-process throttling");
    }

    private static async Task<ConcurrentBag<(int id, int status, long ms)>> SendRequestsAsync(
        int count, string url, bool throttle, int maxConcurrency)
    {
        using var semaphore = throttle ? new SemaphoreSlim(maxConcurrency, maxConcurrency) : null;
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var results = new ConcurrentBag<(int, int, long)>();
        int active = 0;

        var tasks = Enumerable.Range(1, count).Select(async id =>
        {
            if (semaphore is not null)
                await semaphore.WaitAsync();

            int current = Interlocked.Increment(ref active);
            Console.WriteLine($"    [{id}] Start (active={current})");

            var sw = Stopwatch.StartNew();
            int status;
            try
            {
                var response = await http.GetAsync(url);
                status = (int)response.StatusCode;
            }
            catch (Exception ex)
            {
                status = -1;
                Console.WriteLine($"    [{id}] Error: {ex.Message}");
            }
            sw.Stop();

            Interlocked.Decrement(ref active);
            results.Add((id, status, sw.ElapsedMilliseconds));

            Console.WriteLine($"    [{id}] Done  status={status} {sw.ElapsedMilliseconds}ms (active={active})");

            semaphore?.Release();
        });

        await Task.WhenAll(tasks);
        return results;
    }

    private static void PrintResults(ConcurrentBag<(int id, int status, long ms)> results, long totalMs)
    {
        var ok = results.Count(r => r.status == 200);
        var fail = results.Count(r => r.status != 200);
        var avgMs = results.Average(r => r.ms);

        Console.WriteLine($"\n  Total time: {totalMs}ms");
        Console.WriteLine($"  OK (200): {ok}, Failed: {fail}");
        Console.WriteLine($"  Avg request time: {avgMs:F0}ms\n");
    }
}

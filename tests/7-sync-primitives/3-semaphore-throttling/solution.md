# Решение: Многопоточный краулер сайтов с throttling

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

var urls = Enumerable.Range(1, 20).Select(i => $"/page{i}.html").ToList();
var crawler = new WebCrawler();

Console.WriteLine("=== CRAWLING 20 URLs ===\n");

await crawler.CrawlWithoutThrottling(urls);

Console.WriteLine();
await crawler.CrawlWithThrottling(urls, maxConcurrent: 3);

Console.WriteLine();
await crawler.CrawlWithTimeout(urls, maxConcurrent: 3, TimeSpan.FromMilliseconds(100));

Console.WriteLine();
await crawler.CrawlWithCancellation(urls, maxConcurrent: 3);

public class WebCrawler
{
    public async Task CrawlWithoutThrottling(List<string> urls)
    {
        Console.WriteLine("--- Without Throttling ---");
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] Starting ALL {urls.Count} requests simultaneously");

        var tasks = urls.Select(u => CrawlPage(u, 0));
        await Task.WhenAll(tasks);

        sw.Stop();
        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] All {urls.Count} completed in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine("Server load: 20 concurrent requests (DDoS-level!)");
    }

    public async Task CrawlWithThrottling(List<string> urls, int maxConcurrent)
    {
        Console.WriteLine($"--- With Throttling (max {maxConcurrent}) ---");
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] Starting in groups of {maxConcurrent}...");

        var semaphore = new SemaphoreSlim(maxConcurrent);
        var tasks = urls.Select(async url =>
        {
            await semaphore.WaitAsync();
            try
            {
                await CrawlPage(url, sw.ElapsedMilliseconds);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        sw.Stop();
        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] All {urls.Count} completed in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Server load: max {maxConcurrent} concurrent requests (safe!)");
    }

    public async Task CrawlWithTimeout(List<string> urls, int maxConcurrent, TimeSpan timeout)
    {
        Console.WriteLine($"--- With Throttling + Timeout (max {maxConcurrent}, timeout {timeout.TotalMilliseconds}ms) ---");
        var sw = Stopwatch.StartNew();
        int completed = 0;
        int skipped = 0;

        var semaphore = new SemaphoreSlim(maxConcurrent);
        var tasks = urls.Select(async url =>
        {
            bool acquired = await semaphore.WaitAsync(timeout);
            if (!acquired)
            {
                Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] {url} — [SKIPPED] Timeout waiting for slot");
                Interlocked.Increment(ref skipped);
                return;
            }

            try
            {
                await CrawlPage(url, sw.ElapsedMilliseconds);
                Interlocked.Increment(ref completed);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        Console.WriteLine($"Completed: {completed} | Skipped: {skipped} (timeout)");
    }

    public async Task CrawlWithCancellation(List<string> urls, int maxConcurrent)
    {
        Console.WriteLine("--- With Cancellation ---");
        var sw = Stopwatch.StartNew();
        using var cts = new CancellationTokenSource();
        int completed = 0;
        int cancelled = 0;

        var semaphore = new SemaphoreSlim(maxConcurrent);

        var crawlTask = Task.WhenAll(urls.Select(async url =>
        {
            try
            {
                await semaphore.WaitAsync(cts.Token);
                try
                {
                    await CrawlPage(url, sw.ElapsedMilliseconds, cts.Token);
                    Interlocked.Increment(ref completed);
                }
                finally
                {
                    semaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                Interlocked.Increment(ref cancelled);
            }
        }));

        await Task.Delay(800);
        cts.Cancel();
        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] Cancellation requested!");

        try { await crawlTask; } catch (OperationCanceledException) { }

        Console.WriteLine($"Graceful shutdown. Completed: {completed} | Cancelled: {cancelled}");
    }

    private async Task CrawlPage(string url, long startMs, CancellationToken ct = default)
    {
        var rng = new Random(url.GetHashCode());
        int delay = rng.Next(200, 500);
        await Task.Delay(delay, ct);
        Console.WriteLine($"[{startMs + delay}ms] {url} — 200 OK ({delay}ms)");
    }
}
```

## Ключевые моменты

1. **SemaphoreSlim(maxConcurrent)**: ограничивает количество потоков/задач, одновременно входящих в критическую секцию. `WaitAsync()` — асинхронная версия, не блокирует поток.

2. **Throttling HTTP**: запускаем 20 запросов, но только N одновременно. Остальные ждут освобождения слота. Сервер не перегружается.

3. **WaitAsync(timeout)**: если слот не освободился за указанное время — возвращает `false`. Можно пропустить URL вместо бесконечного ожидания.

4. **WaitAsync(ct)**: поддержка CancellationToken. При отмене бросает `OperationCanceledException`. Идеально для graceful shutdown.

5. **Release() в finally**: всегда освобождать семафор, даже при исключении. Иначе слоты будут «утекать» и семафор истощится.

# Решение: Анализатор логов веб-сервера

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

int logCount = 5_000_000; // можно уменьшить до 500_000 для быстрого теста
Console.WriteLine($"Generating {logCount:N0} fake log lines...");
var logs = LogAnalyzer.GenerateFakeLogs(logCount);
Console.WriteLine("Done generating.\n");

var analyzer = new LogAnalyzer();
analyzer.RunBenchmark(logs);

public class LogAnalyzer
{
    public static List<string> GenerateFakeLogs(int count)
    {
        var rng = new Random(42);
        var methods = new[] { "GET", "POST", "PUT", "DELETE" };
        var resources = new[] { "users", "orders", "products", "reports", "auth", "payments", "search", "inventory" };
        var logs = new List<string>(count);

        for (int i = 0; i < count; i++)
        {
            int subnet = rng.Next(1, 255);
            int host = rng.Next(1, 255);
            int min = rng.Next(0, 60);
            int sec = rng.Next(0, 60);
            string method = methods[rng.Next(methods.Length)];
            string resource = resources[rng.Next(resources.Length)];
            int id = rng.Next(1, 10_000);

            int statusRoll = rng.Next(100);
            int status = statusRoll < 80 ? rng.Next(200, 300) :
                         statusRoll < 95 ? rng.Next(400, 500) :
                         rng.Next(500, 600);

            int time = rng.Next(1, 1001);

            logs.Add(
                $"192.168.{subnet}.{host} [11/Jul/2026:14:{min:D2}:{sec:D2}] \"{method} /api/{resource}/{id} HTTP/1.1\" {status} {time}ms");
        }

        return logs;
    }

    public void RunBenchmark(List<string> logs)
    {
        int cores = Environment.ProcessorCount;
        Console.WriteLine($"=== BENCHMARK: {logs.Count:N0} log lines | CPU cores: {cores} ===\n");

        GC.Collect();
        GC.WaitForPendingFinalizers();

        var sw = Stopwatch.StartNew();
        var statsSeq = AnalyzeSequential(logs);
        sw.Stop();
        long seqTime = sw.ElapsedMilliseconds;
        Console.WriteLine($"[SEQUENTIAL]  Processed {logs.Count:N0} rows in {seqTime}ms");
        ShowStats(statsSeq);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        sw.Restart();
        var statsConc = AnalyzeConcurrent(logs);
        sw.Stop();
        long concTime = sw.ElapsedMilliseconds;
        double concSpeedup = seqTime / (double)concTime;
        Console.WriteLine($"\n[CONCURRENT]  Processed {logs.Count:N0} rows in {concTime}ms (speedup: {concSpeedup:F1}x)");
        ShowStats(statsConc);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        sw.Restart();
        var statsPar = AnalyzeParallel(logs);
        sw.Stop();
        long parTime = sw.ElapsedMilliseconds;
        double parSpeedup = seqTime / (double)parTime;
        Console.WriteLine($"\n[PARALLEL]    Processed {logs.Count:N0} rows in {parTime}ms (speedup: {parSpeedup:F1}x)");
        Console.WriteLine($"              Parallel.For on {cores} cores — true parallelism");
        ShowStats(statsPar);
    }

    private void ShowStats(LogStats stats)
    {
        Console.WriteLine($"  Requests: {stats.TotalCount:N0} | Errors(5xx): {stats.ErrorCount:N0} ({stats.ErrorPercent:F2}%)");
        Console.WriteLine("  Methods: " + string.Join(", ",
            stats.MethodCounts.Select(kv => $"{kv.Key}: {kv.Value:N0}")));

        var top10 = stats.SlowestUrls.OrderByDescending(kv => kv.Value).Take(10).ToList();
        Console.WriteLine("  Top-5 slowest URLs:");
        int rank = 1;
        foreach (var (url, time) in top10.Take(5))
        {
            Console.WriteLine($"    {rank++}. {url,-40} {time}ms");
        }
    }

    public LogStats AnalyzeSequential(List<string> logs)
    {
        var stats = new LogStats();
        foreach (string line in logs)
        {
            ProcessLine(line, stats);
        }
        return stats;
    }

    public LogStats AnalyzeConcurrent(List<string> logs)
    {
        int chunkCount = Math.Min(4, Environment.ProcessorCount);
        int chunkSize = logs.Count / chunkCount;
        var chunks = new LogStats[chunkCount];

        using var countdown = new CountdownEvent(chunkCount);
        var threadIds = new ConcurrentDictionary<int, byte>();

        for (int i = 0; i < chunkCount; i++)
        {
            int chunkIndex = i;
            int start = chunkIndex * chunkSize;
            int end = (chunkIndex == chunkCount - 1) ? logs.Count : start + chunkSize;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                threadIds.TryAdd(Environment.CurrentManagedThreadId, 0);
                var localStats = new LogStats();
                for (int j = start; j < end; j++)
                {
                    ProcessLine(logs[j], localStats);
                }
                chunks[chunkIndex] = localStats;
                countdown.Signal();
            });
        }

        countdown.Wait();

        Console.WriteLine($"  Threads used: [{string.Join(", ", threadIds.Keys)}] — concurrency, not true parallelism");

        return AggregateStats(chunks);
    }

    public LogStats AnalyzeParallel(List<string> logs)
    {
        var stats = new LogStats();
        var localMethodCounts = new ConcurrentDictionary<string, int>();
        var localSlowUrls = new ConcurrentDictionary<string, int>();

        int totalCount = logs.Count;
        int errorCount = 0;
        int getCount = 0, postCount = 0, putCount = 0, deleteCount = 0;

        Parallel.For(0, totalCount, () => new ThreadLocalStats(), (i, _, local) =>
        {
            string line = logs[i];
            ParseLine(line, out string method, out string url, out int status, out int timeMs);

            local.Count++;

            if (status >= 500) local.Errors++;

            switch (method)
            {
                case "GET": local.GetCount++; break;
                case "POST": local.PostCount++; break;
                case "PUT": local.PutCount++; break;
                case "DELETE": local.DeleteCount++; break;
            }

            if (timeMs > 800)
                local.SlowUrls[url] = timeMs;

            return local;
        },
        local =>
        {
            Interlocked.Add(ref totalCount, local.Count);
            Interlocked.Add(ref errorCount, local.Errors);
            Interlocked.Add(ref getCount, local.GetCount);
            Interlocked.Add(ref postCount, local.PostCount);
            Interlocked.Add(ref putCount, local.PutCount);
            Interlocked.Add(ref deleteCount, local.DeleteCount);

            foreach (var (url, time) in local.SlowUrls)
            {
                localSlowUrls.AddOrUpdate(url, time, (_, existing) => Math.Max(existing, time));
            }
        });

        stats.TotalCount = totalCount;
        stats.ErrorCount = errorCount;
        stats.ErrorPercent = stats.TotalCount > 0 ? (double)stats.ErrorCount / stats.TotalCount * 100 : 0;
        stats.MethodCounts["GET"] = getCount;
        stats.MethodCounts["POST"] = postCount;
        stats.MethodCounts["PUT"] = putCount;
        stats.MethodCounts["DELETE"] = deleteCount;
        stats.SlowestUrls = new Dictionary<string, int>(localSlowUrls);

        return stats;
    }

    private void ParseLine(string line, out string method, out string url, out int status, out int timeMs)
    {
        method = "GET";
        url = "";
        status = 200;
        timeMs = 0;

        int methodStart = line.IndexOf('"') + 1;
        int methodEnd = line.IndexOf(' ', methodStart);
        if (methodStart > 0 && methodEnd > methodStart)
            method = line.Substring(methodStart, methodEnd - methodStart);

        int urlStart = methodEnd + 1;
        int urlEnd = line.IndexOf(' ', urlStart);
        if (urlStart > 0 && urlEnd > urlStart)
            url = line.Substring(urlStart, urlEnd - urlStart);

        int statusStart = line.IndexOf("\" ", StringComparison.Ordinal) + 2;
        if (statusStart > 1)
        {
            int statusEnd = line.IndexOf(' ', statusStart);
            if (statusEnd > statusStart)
                int.TryParse(line.Substring(statusStart, statusEnd - statusStart), out status);
        }

        int msIdx = line.LastIndexOf("ms", StringComparison.Ordinal);
        if (msIdx > 0)
        {
            int timeStart = line.LastIndexOf(' ', msIdx) + 1;
            string timeStr = line.Substring(timeStart, msIdx - timeStart);
            int.TryParse(timeStr, out timeMs);
        }
    }

    private void ProcessLine(string line, LogStats stats)
    {
        ParseLine(line, out string method, out string url, out int status, out int timeMs);

        stats.TotalCount++;
        if (status >= 500) stats.ErrorCount++;

        if (stats.MethodCounts.TryGetValue(method, out int current))
            stats.MethodCounts[method] = current + 1;
        else
            stats.MethodCounts[method] = 1;

        if (timeMs > 800)
        {
            if (!stats.SlowestUrls.TryGetValue(url, out int existing) || timeMs > existing)
                stats.SlowestUrls[url] = timeMs;
        }
    }

    private LogStats AggregateStats(LogStats[] chunks)
    {
        var result = new LogStats();
        foreach (var chunk in chunks)
        {
            if (chunk == null) continue;
            result.TotalCount += chunk.TotalCount;
            result.ErrorCount += chunk.ErrorCount;
            foreach (var (method, count) in chunk.MethodCounts)
            {
                if (result.MethodCounts.TryGetValue(method, out int current))
                    result.MethodCounts[method] = current + count;
                else
                    result.MethodCounts[method] = count;
            }
            foreach (var (url, time) in chunk.SlowestUrls)
            {
                if (!result.SlowestUrls.TryGetValue(url, out int existing) || time > existing)
                    result.SlowestUrls[url] = time;
            }
        }
        result.ErrorPercent = result.TotalCount > 0 ? (double)result.ErrorCount / result.TotalCount * 100 : 0;
        return result;
    }
}

public class LogStats
{
    public int TotalCount;
    public int ErrorCount;
    public double ErrorPercent;
    public Dictionary<string, int> MethodCounts = new();
    public Dictionary<string, int> SlowestUrls = new();
}

public class ThreadLocalStats
{
    public int Count;
    public int Errors;
    public int GetCount;
    public int PostCount;
    public int PutCount;
    public int DeleteCount;
    public Dictionary<string, int> SlowUrls = new();
}
```

## Ключевые моменты

### Почему Concurrent быстрее Sequential, но Parallelism ещё быстрее?

- **Sequential** — один поток обрабатывает всё подряд. CPU простаивает, когда есть другие свободные ядра.
- **Concurrent** — делим работу на чанки, каждый на своём потоке ThreadPool. На 4-ядерной машине 4 потока могут чередоваться на 4 ядрах, но НЕ работают одновременно на одном ядре. Ускорение есть (нет полного простоя), но ограничено количеством ядер.
- **Parallelism** — `Parallel.For` автоматически распределяет итерации по ВСЕМ доступным ядрам. Каждое ядро обрабатывает свой кусок данных ОДНОВРЕМЕННО. Для CPU-bound задач это максимальное ускорение.

### Когда Concurrency ЛУЧШЕ Parallelism?

Когда задача **IO-bound**, а не CPU-bound. Например, 100 HTTP-запросов к API. Если запустить их параллельно (ThreadPool), они все будут ждать сеть. Но если использовать Concurrency (async/await + один поток), мы экономим ресурсы — один поток может обслужить тысячи ожидающих запросов без лишних потоков и переключений контекста.

### Interlocked vs lock в Parallel.For
- `Interlocked.Increment` — lock-free атомарная операция, быстрее чем `lock` для простых операций
- `ThreadLocal<T>` аккумуляторы в `Parallel.For` — каждый поток накапливает статистику локально, затем финальная агрегация. Это минимизирует синхронизацию.

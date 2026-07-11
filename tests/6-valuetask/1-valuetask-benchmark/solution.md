# Решение: Кэш сессий — Task vs ValueTask benchmark

```csharp
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

Console.WriteLine("=== BENCHMARK: 1,000,000 sessions | Cache hit rate: 95% ===\n");

var cache = new SessionCache();

// Pre-populate cache for 95% hit rate
int cacheSize = 950_000;
Console.WriteLine($"Pre-populating cache with {cacheSize:N0} entries...");
for (int i = 0; i < cacheSize; i++)
    cache.Set(i, new Session(i, $"user_{i}", DateTime.UtcNow));
Console.WriteLine("Done.\n");

int iterations = 1_000_000;
cache.RunBenchmark(iterations);

Console.WriteLine("\n=== ValueTask Limitations ===\n");
cache.DemonstrateValueTaskLimitations();

public record Session(int UserId, string Username, DateTime LoginTime);

public class SessionCache
{
    private readonly ConcurrentDictionary<int, Session> _cache = new();
    private int _nextMissId = 1_000_000;

    public void Set(int userId, Session session) => _cache[userId] = session;

    public async Task<Session> GetSessionWithTask(int userId)
    {
        if (_cache.TryGetValue(userId, out var session))
            return session; // synchronous — but still allocates Task<Session>!

        session = await FetchFromDbAsync(userId);
        _cache[userId] = session;
        return session;
    }

    public async ValueTask<Session> GetSessionWithValueTask(int userId)
    {
        if (_cache.TryGetValue(userId, out var session))
            return session; // synchronous — NO heap allocation!

        session = await FetchFromDbAsync(userId);
        _cache[userId] = session;
        return session;
    }

    public void RunBenchmark(int iterations)
    {
        // Task<Session> benchmark
        ForceGC();
        long memBefore = GC.GetTotalMemory(true);
        var sw = Stopwatch.StartNew();

        var taskTasks = new Task[iterations];
        for (int i = 0; i < iterations; i++)
        {
            int userId = GetRandomUserId(i);
            taskTasks[i] = GetSessionWithTask(userId);
        }
        Task.WaitAll(taskTasks);

        sw.Stop();
        long memAfter = GC.GetTotalMemory(false);
        long taskAlloc = memAfter - memBefore;

        Console.WriteLine("--- Task<Session> ---");
        Console.WriteLine($"  Time: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Allocations: {taskAlloc / 1024:N0} KB (~{taskAlloc / 1024 / 1024} MB)");

        // ValueTask<Session> benchmark
        ForceGC();
        memBefore = GC.GetTotalMemory(true);
        sw.Restart();

        for (int i = 0; i < iterations; i++)
        {
            int userId = GetRandomUserId(i);
            ValueTask<Session> vt = GetSessionWithValueTask(userId);
            if (!vt.IsCompleted)
                vt.AsTask().Wait();
        }

        sw.Stop();
        memAfter = GC.GetTotalMemory(false);
        long vtAlloc = memAfter - memBefore;
        long savings = taskAlloc - vtAlloc;

        Console.WriteLine("\n--- ValueTask<Session> ---");
        Console.WriteLine($"  Time: {sw.ElapsedMilliseconds}ms (speedup: {taskAlloc / (double)Math.Max(vtAlloc, 1):F1}x in memory)");
        Console.WriteLine($"  Allocations: {vtAlloc / 1024:N0} KB (~{vtAlloc / 1024 / 1024} MB)");
        Console.WriteLine($"  Memory saved: {savings / 1024:N0} KB (~{savings * 100.0 / Math.Max(taskAlloc, 1):F0}% savings!)");
    }

    public async Task DemonstrateValueTaskLimitations()
    {
        Console.WriteLine("1. Double await:");
        ValueTask<int> vt = GetValueAsync();
        await vt;
        try
        {
            await vt;
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"   Exception: {ex.Message}");
            Console.WriteLine("   Fix: Don't await twice, or use .AsTask() (but that allocates).");
        }

        Console.WriteLine("\n2. WhenAll:");
        ValueTask<int> vt1 = new(42);
        ValueTask<int> vt2 = new(100);
        // Task.WhenAll(vt1, vt2) — compile error!
        await Task.WhenAll(vt1.AsTask(), vt2.AsTask());
        Console.WriteLine("   Workaround: vt1.AsTask(), vt2.AsTask() — but each .AsTask() allocates!");

        Console.WriteLine("\n3. AsTask() penalty:");
        Console.WriteLine("   .AsTask() creates a new Task wrapper — loses the ValueTask advantage.");
    }

    private async ValueTask<int> GetValueAsync()
    {
        await Task.Delay(10);
        return 42;
    }

    private int GetRandomUserId(int index)
    {
        // 95% cache hit
        return index % 20 < 19
            ? index % 950_000
            : Interlocked.Increment(ref _nextMissId);
    }

    private async Task<Session> FetchFromDbAsync(int userId)
    {
        await Task.Delay(5);
        return new Session(userId, $"user_{userId}", DateTime.UtcNow);
    }

    private static void ForceGC()
    {
        GC.Collect(2, GCCollectionMode.Forced);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced);
    }
}
```

## Ключевые моменты

1. **Task<T> всегда аллоцирует**: класс в heap (~48 байт). Даже для синхронных завершений (`return value`) создаётся новый Task. При миллионах вызовов это создаёт GC pressure.

2. **ValueTask<T> — struct**: 8 байт на стеке. Для синхронных завершений ноль аллокаций. Для асинхронных — содержит ссылку на Task (аллокация всё равно есть, но только для 5% промахов).

3. **Ограничения**:
   - Двойной await запрещён
   - `Task.WhenAll` не работает напрямую (нужно `.AsTask()`)
   - `.AsTask()` создаёт Task — теряется смысл ValueTask

4. **Когда использовать**: метод завершается синхронно >90% случаев, hot path, миллионы вызовов. Во всех остальных случаях — `Task`.

5. **IValueTaskSource**: для продвинутых сценариев (Socket, FileStream) позволяет переиспользовать объекты и избегать аллокаций даже для асинхронных завершений.

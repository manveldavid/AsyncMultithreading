# Решение: Сервис массовой рассылки email-уведомлений

```csharp
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

var dispatcher = new EmailDispatcher();

Console.WriteLine("=== Sending emails via ThreadPool ===\n");
dispatcher.SendAllEmails(200);

Console.WriteLine("\n=== STARVATION DEMO ===\n");
dispatcher.SimulateStarvation(blockCount: 10);

Console.WriteLine("\n=== QueueUserWorkItem vs Unsafe ===\n");
dispatcher.CompareQueueUserWorkItemVsUnsafe();

Console.WriteLine("\n=== Hill Climbing Effect ===\n");
dispatcher.ShowHillClimbingEffect();

public class EmailDispatcher
{
    private readonly ConcurrentDictionary<int, byte> _usedThreads = new();

    public void SendAllEmails(int emailCount)
    {
        ShowThreadPoolStats("before");
        _usedThreads.Clear();

        using var countdown = new CountdownEvent(emailCount);
        var rng = new Random();

        for (int i = 1; i <= emailCount; i++)
        {
            int emailId = i;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                int threadId = Environment.CurrentManagedThreadId;
                _usedThreads.TryAdd(threadId, 0);

                int delay = rng.Next(50, 151);
                Thread.Sleep(delay);

                Console.WriteLine(
                    $"[ThreadPool-{threadId}] Sending email to user_{emailId}@company.com... Done ({delay}ms)");

                countdown.Signal();
            });
        }

        countdown.Wait();
        ShowThreadPoolStats("after");
        Console.WriteLine($"Unique threads used: {_usedThreads.Count}");
    }

    public void SimulateStarvation(int blockCount)
    {
        ThreadPool.GetMinThreads(out int minWorker, out _);
        Console.WriteLine($"Min worker threads: {minWorker}");
        Console.WriteLine($"Blocking {blockCount} threads for 3000ms...\n");

        _usedThreads.Clear();

        for (int i = 0; i < blockCount; i++)
        {
            int blockId = i;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                int threadId = Environment.CurrentManagedThreadId;
                _usedThreads.TryAdd(threadId, 0);
                Console.WriteLine($"[ThreadPool-{threadId}] BLOCKED #{blockId}");
                Thread.Sleep(3000);
                Console.WriteLine($"[ThreadPool-{threadId}] UNBLOCKED #{blockId}");
            });
        }

        Thread.Sleep(200);

        var urgentDone = 0;
        var urgentLock = new object();
        int urgentCount = 20;

        using var urgentCde = new CountdownEvent(urgentCount);

        for (int i = 0; i < urgentCount; i++)
        {
            int urgentId = i;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                int threadId = Environment.CurrentManagedThreadId;
                Thread.Sleep(50);
                lock (urgentLock) { urgentDone++; }
                Console.WriteLine($"[ThreadPool-{threadId}] URGENT email #{urgentId} sent!");
                urgentCde.Signal();
            });
        }

        Thread.Sleep(2000);
        int completed = urgentDone;
        Console.WriteLine($"\nUrgent emails sent in first 2 seconds: {completed}/{urgentCount}");
        if (completed < urgentCount)
            Console.WriteLine("ThreadPool STARVATION detected: new threads created at ~1/sec (injection rate)");

        urgentCde.Wait();
        Console.WriteLine("All urgent emails eventually sent.");
    }

    public void CompareQueueUserWorkItemVsUnsafe()
    {
        const int iterations = 10_000;
        using var countdown = new CountdownEvent(iterations);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Yield();
                countdown.Signal();
            });
        }
        countdown.Wait();
        sw.Stop();
        long qwuiTime = sw.ElapsedMilliseconds;
        Console.WriteLine($"QueueUserWorkItem:       {qwuiTime}ms");

        countdown.Reset(iterations);
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                Thread.Yield();
                countdown.Signal();
            });
        }
        countdown.Wait();
        sw.Stop();
        long uqwuiTime = sw.ElapsedMilliseconds;
        Console.WriteLine($"UnsafeQueueUserWorkItem: {uqwuiTime}ms");
        Console.WriteLine($"Speedup: {qwuiTime / (double)uqwuiTime:F1}x");
        Console.WriteLine("(Unsafe skips ExecutionContext capture — faster but no security flow)");
    }

    public void ShowHillClimbingEffect()
    {
        ThreadPool.GetMinThreads(out int minBefore, out _);
        ThreadPool.GetMaxThreads(out int maxBefore, out _);
        ThreadPool.GetAvailableThreads(out int availBefore, out _);
        Console.WriteLine($"Before: Min={minBefore}, Max={maxBefore}, Available={availBefore}");

        int taskCount = 50;
        using var countdown = new CountdownEvent(taskCount);

        for (int i = 0; i < taskCount; i++)
        {
            int id = i;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(500);
                countdown.Signal();
            });
        }

        Thread.Sleep(800);

        ThreadPool.GetAvailableThreads(out int availDuring, out _);
        int busyDuring = maxBefore - availDuring;
        Console.WriteLine($"During load: Available={availDuring}, Busy={busyDuring}");
        Console.WriteLine("ThreadPool is adding threads (Hill Climbing algorithm)");

        countdown.Wait();

        ThreadPool.GetAvailableThreads(out int availAfter, out _);
        int busyAfter = maxBefore - availAfter;
        Console.WriteLine($"After load: Available={availAfter}, Busy={busyAfter}");
        Console.WriteLine("Hill Climbing: ThreadPool dynamically adjusts thread count based on workload.");
        Console.WriteLine("When tasks block (Thread.Sleep), TP detects the need and injects more threads.");
    }

    private void ShowThreadPoolStats(string label)
    {
        ThreadPool.GetMinThreads(out int minWorker, out _);
        ThreadPool.GetMaxThreads(out int maxWorker, out _);
        ThreadPool.GetAvailableThreads(out int availWorker, out _);
        int busy = maxWorker - availWorker;

        Console.WriteLine($"=== ThreadPool Stats ({label}) ===");
        Console.WriteLine($"Min worker: {minWorker} | Max: {maxWorker}");
        Console.WriteLine($"Available: {availWorker} | Busy: {busy}");
        Console.WriteLine();
    }
}
```

## Ключевые моменты

1. **ThreadPool.QueueUserWorkItem** — запускает задачу на свободном потоке пула. Потоки переиспользуются: для 200 писем будет задействовано ~12 уникальных потоков, а не 200.

2. **ThreadPool Starvation** — когда все потоки заняты блокирующими операциями, новые задачи не могут стартовать. ThreadPool добавляет только ~1 поток в секунду (injection rate). Поэтому из 20 срочных писем за первые 2 секунды выполнится лишь малая часть.

3. **UnsafeQueueUserWorkItem vs QueueUserWorkItem** — `Unsafe` не копирует `ExecutionContext` (security principal, culture, async locals), поэтому работает быстрее. Но это опасно: если вы полагаетесь на flowing контекста (например, `AsyncLocal`), `Unsafe` его потеряет.

4. **Hill Climbing** — алгоритм динамической подстройки размера пула. Когда ThreadPool видит, что потоки блокируются, он постепенно добавляет новые, чтобы максимизировать throughput.

5. **CountdownEvent** — используется для ожидания завершения N задач без блокировки каждого потока по отдельности. Более эффективная альтернатива `Join()` для ThreadPool (где нет ссылок на потоки).

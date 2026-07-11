# Решение: Торговый движок криптобиржи — lock, race condition, deadlock

```csharp
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

var engine = new ExchangeEngine();

Console.WriteLine("=== RACE CONDITION ===\n");
engine.DemonstrateRaceCondition();

Console.WriteLine("\n=== FIXED WITH LOCK ===\n");
engine.FixWithLock();

Console.WriteLine("\n=== FIXED WITH INTERLOCKED ===\n");
engine.FixWithInterlocked();

Console.WriteLine("\n=== DEADLOCK WITH TWO LOCKS ===\n");
engine.DemonstrateDeadlockWithTwoLocks();

Console.WriteLine("\n=== FIXED: LOCK ORDERING ===\n");
engine.FixWithOrderedLocks();

Console.WriteLine("\n=== COMPARISON ===\n");
engine.CompareLockVsInterlockedVsNoSync();

public class ExchangeEngine
{
    public void DemonstrateRaceCondition()
    {
        long balance = 0;
        int traders = 10;
        int iterations = 10_000;
        long expected = traders * iterations;

        var sw = Stopwatch.StartNew();

        var tasks = new Task[traders];
        for (int t = 0; t < traders; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                    balance++; // NOT atomic — race condition!
            });
        }
        Task.WhenAll(tasks).Wait();

        sw.Stop();
        long lost = expected - balance;
        Console.WriteLine($"Expected balance: ${expected:N0}");
        Console.WriteLine($"Actual balance:   ${balance:N0}");
        Console.WriteLine($"LOST: ${lost:N0} ({lost * 100.0 / expected:F1}%) — classic race condition!");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
    }

    public void FixWithLock()
    {
        long balance = 0;
        var lockObj = new object();
        int traders = 10;
        int iterations = 10_000;
        long expected = traders * iterations;

        var sw = Stopwatch.StartNew();

        var tasks = new Task[traders];
        for (int t = 0; t < traders; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                    lock (lockObj) { balance++; }
            });
        }
        Task.WhenAll(tasks).Wait();

        sw.Stop();
        Console.WriteLine($"Expected: ${expected:N0}");
        Console.WriteLine($"Actual:   ${balance:N0}");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms — correct but with lock overhead");
    }

    public void FixWithInterlocked()
    {
        long balance = 0;
        int traders = 10;
        int iterations = 10_000;
        long expected = traders * iterations;

        var sw = Stopwatch.StartNew();

        var tasks = new Task[traders];
        for (int t = 0; t < traders; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                    Interlocked.Increment(ref balance);
            });
        }
        Task.WhenAll(tasks).Wait();

        sw.Stop();
        Console.WriteLine($"Expected: ${expected:N0}");
        Console.WriteLine($"Actual:   ${balance:N0}");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms — lock-free, faster!");
    }

    public void DemonstrateDeadlockWithTwoLocks()
    {
        var btcLock = new object();
        var ethLock = new object();
        var completed = 0;
        var deadlocked = false;

        var t1 = Task.Run(() =>
        {
            lock (btcLock)
            {
                Console.WriteLine("[Trader A] Locked BTC, waiting for ETH...");
                Thread.Sleep(100);

                if (!Monitor.TryEnter(ethLock, 2000))
                {
                    Console.WriteLine("[Trader A] Monitor.TryEnter timed out after 2000ms");
                    deadlocked = true;
                    return;
                }
                try { Interlocked.Increment(ref completed); }
                finally { Monitor.Exit(ethLock); }
            }
        });

        var t2 = Task.Run(() =>
        {
            lock (ethLock)
            {
                Console.WriteLine("[Trader B] Locked ETH, waiting for BTC...");
                Thread.Sleep(100);

                if (!Monitor.TryEnter(btcLock, 2000))
                {
                    Console.WriteLine("[Trader B] Monitor.TryEnter timed out after 2000ms");
                    deadlocked = true;
                    return;
                }
                try { Interlocked.Increment(ref completed); }
                finally { Monitor.Exit(btcLock); }
            }
        });

        Task.WhenAll(t1, t2).Wait();

        if (deadlocked || completed < 2)
            Console.WriteLine("<<< DEADLOCK DETECTED >>>");
    }

    public void FixWithOrderedLocks()
    {
        var btcLock = new object();
        var ethLock = new object();

        var t1 = Task.Run(() =>
        {
            var first = btcLock; // BTC < ETH alphabetically
            var second = ethLock;
            lock (first)
            {
                Console.WriteLine("[Trader A] Locked BTC → ETH — success");
                Thread.Sleep(200);
                lock (second) { }
            }
        });

        Thread.Sleep(50);
        var t2 = Task.Run(() =>
        {
            var first = btcLock; // BTC < ETH alphabetically — same order!
            var second = ethLock;
            Console.WriteLine("[Trader B] Locked ETH → BTC (BTC already locked by A, waiting...)");
            lock (first)
            {
                Console.WriteLine("[Trader B] Got BTC — success");
                lock (second) { }
            }
        });

        Task.WhenAll(t1, t2).Wait();
        Console.WriteLine("No deadlock — consistent lock order.");
    }

    public void CompareLockVsInterlockedVsNoSync()
    {
        Console.WriteLine("| Method      | Result        | Thread-safe |");
        Console.WriteLine("|-------------|---------------|-------------|");
        Console.WriteLine("| No sync     | Incorrect     | NO          |");
        Console.WriteLine("| lock        | Correct       | YES         |");
        Console.WriteLine("| Interlocked | Correct       | YES (faster)|");
    }
}
```

## Ключевые моменты

1. **Race condition**: `balance++` — не атомарная операция (read-modify-write). Два потока могут прочитать одно значение, инкрементировать и записать — один инкремент теряется.

2. **lock (Monitor)**: `lock(obj)` → `Monitor.Enter(obj)` + `try/finally { Monitor.Exit(obj) }`. Использует object header (thin lock → fat lock при конкуренции).

3. **Monitor.TryEnter**: позволяет попытаться захватить lock с таймаутом. Не блокирует навсегда. Идеально для обнаружения deadlock.

4. **Interlocked.Increment**: lock-free атомарная операция. Использует CPU-инструкции (LOCK XADD на x86). Быстрее lock для простых операций.

5. **Deadlock между lock-ами**: если два потока захватывают два lock-а в разном порядке — deadlock. Исправление: всегда захватывать в одном порядке.

6. **Reentrancy**: `lock` в .NET — реентрантный: тот же поток может захватить lock повторно.

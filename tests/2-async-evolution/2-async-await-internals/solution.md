# Решение: Симулятор банковских транзакций

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

Console.WriteLine("=== SynchronizationContext Flow ===\n");
var processor = new TransactionProcessor();
await processor.ShowContextFlow();

Console.WriteLine("\n=== ConfigureAwait(false) Comparison ===\n");
await processor.DemonstrateConfigureAwait();

Console.WriteLine("\n=== Custom SingleThreadSynchronizationContext ===\n");
await processor.EmulateSynchronizationContext();

Console.WriteLine("\n=== State Machine Trace ===\n");
var tx = new Transaction(1, "ACC-001", "ACC-002", 1500.00m);
await processor.AuthorizeTransactionWithTraceAsync(tx);

public record Transaction(int Id, string AccountFrom, string AccountTo, decimal Amount);

public class TransactionProcessor
{
    public async Task ShowContextFlow()
    {
        Console.WriteLine($"SynchronizationContext.Current = {SynchronizationContext.Current?.GetType().Name ?? "null"}");
        Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] Before await");

        await Task.Delay(50);

        Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] After await");
        Console.WriteLine($"SynchronizationContext.Current = {SynchronizationContext.Current?.GetType().Name ?? "null"}");
        Console.WriteLine("Console app has null context → continuation runs on ThreadPool (any thread).");
    }

    public async Task DemonstrateConfigureAwait()
    {
        Console.WriteLine("--- Without ConfigureAwait(false) ---");
        var switchesWithout = new ConcurrentBag<bool>();
        for (int i = 0; i < 5; i++)
        {
            int before = Environment.CurrentManagedThreadId;
            await Task.Delay(20);
            int after = Environment.CurrentManagedThreadId;
            switchesWithout.Add(before != after);
            Console.WriteLine($"  Switches: before=[{before}] → after=[{after}] (changed: {before != after})");
        }

        Console.WriteLine($"\n--- With ConfigureAwait(false) ---");
        var switchesWith = new ConcurrentBag<bool>();
        for (int i = 0; i < 5; i++)
        {
            int before = Environment.CurrentManagedThreadId;
            await Task.Delay(20).ConfigureAwait(false);
            int after = Environment.CurrentManagedThreadId;
            switchesWith.Add(before != after);
            Console.WriteLine($"  Switches: before=[{before}] → after=[{after}] (changed: {before != after})");
        }

        Console.WriteLine($"\nWithout CA(false): {switchesWithout.Count(s => s)}/5 thread switches");
        Console.WriteLine($"With CA(false):    {switchesWith.Count(s => s)}/5 thread switches");
        Console.WriteLine("ConfigureAwait(false) tells runtime: 'don't capture SynchronizationContext'.");
        Console.WriteLine("In Console (null context), both behave similarly. In UI apps, the difference is critical.");
    }

    public async Task EmulateSynchronizationContext()
    {
        var ctx = new SingleThreadSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(ctx);

        var result = await Task.Run(async () =>
        {
            Console.WriteLine($"[Worker:{Environment.CurrentManagedThreadId}] Before await — inside custom context");
            SynchronizationContext.SetSynchronizationContext(ctx);

            await Task.Delay(100);
            Console.WriteLine($"[Worker:{Environment.CurrentManagedThreadId}] After await — SAME thread! Context captured continuation.");

            await Task.Delay(100).ConfigureAwait(false);
            Console.WriteLine($"[Worker:{Environment.CurrentManagedThreadId}] With ConfigureAwait(false) — continuation on ThreadPool, NOT worker!");
        });

        ctx.Stop();
        SynchronizationContext.SetSynchronizationContext(null);
    }

    public async Task AuthorizeTransactionWithTraceAsync(Transaction tx)
    {
        Console.WriteLine($"\n[Thread {Environment.CurrentManagedThreadId}] Starting AuthorizeTransactionAsync for Tx#{tx.Id}");
        Console.WriteLine($"  Amount: {tx.Amount} | From: {tx.AccountFrom} → To: {tx.AccountTo}");

        Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}]   Step 1: CheckBalanceAsync — BEFORE await");
        decimal balance = await CheckBalanceAsync(tx.AccountFrom);
        Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}]   Step 1: CheckBalanceAsync — AFTER await, balance={balance}");

        Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}]   Step 2: AntiFraudCheckAsync — BEFORE await");
        bool isFraud = await AntiFraudCheckAsync(tx);
        Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}]   Step 2: AntiFraudCheckAsync — AFTER await, isFraud={isFraud}");

        Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}]   Step 3: CommitTransactionAsync — BEFORE await");
        await CommitTransactionAsync(tx);
        Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}]   Step 3: CommitTransactionAsync — AFTER await, DONE!");

        Console.WriteLine($"\nCompiler generates state machine with states: -1(initial) → 0(after balance check) → 1(after fraud check) → 2(after commit)");
    }

    private async Task<decimal> CheckBalanceAsync(string account) { await Task.Delay(50); return 5000.00m; }
    private async Task<bool> AntiFraudCheckAsync(Transaction tx) { await Task.Delay(80); return false; }
    private async Task CommitTransactionAsync(Transaction tx) { await Task.Delay(30); }
}

public class SingleThreadSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback, object?)> _queue = new();
    private readonly Thread _worker;
    private volatile bool _running = true;

    public SingleThreadSynchronizationContext()
    {
        _worker = new Thread(RunLoop) { IsBackground = true, Name = "SingleThreadCtx" };
        _worker.Start();
    }

    private void RunLoop()
    {
        while (_running)
        {
            if (_queue.TryTake(out var item, TimeSpan.FromMilliseconds(100)))
            {
                try { item.Item1(item.Item2); }
                catch (Exception ex) { Console.WriteLine($"[Context] Exception: {ex.Message}"); }
            }
        }
    }

    public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

    public override void Send(SendOrPostCallback d, object? state)
    {
        using var mre = new ManualResetEventSlim();
        _queue.Add((s =>
        {
            try { d(s); }
            finally { mre.Set(); }
        }, state));
        mre.Wait();
    }

    public void Stop() { _running = false; _worker.Join(1000); }

    public void Dispose() => Stop();
}
```

## Ключевые моменты

1. **State Machine**: Компилятор превращает `async` метод в структуру `IAsyncStateMachine`. Каждый `await` — точка приостановки. `MoveNext()` вызывается при возобновлении. Номер состояния (state) в switch определяет, с какого места продолжать.

2. **SynchronizationContext**: В Console — `null`, продолжение на ThreadPool (любой поток). В WPF/WinForms — продолжение в UI-потоке. В ASP.NET Core — нет контекста.

3. **ConfigureAwait(false)**: Говорит рантайму «не захватывай SynchronizationContext для продолжения». В UI-приложениях это предотвращает deadlock. В библиотеках — обязательно. В ASP.NET Core — не нужно (контекста нет).

4. **Кастомный SynchronizationContext**: Очередь + выделенный поток — аналог UI message loop. Продолжение после `await` приходит в `Post()`, который добавляет задачу в очередь, и поток её выполняет.

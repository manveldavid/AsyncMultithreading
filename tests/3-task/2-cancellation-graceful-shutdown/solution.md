# Решение: Фоновый сервис экспорта отчётов с graceful shutdown

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

var reports = new[] { 1, 2, 3, 4, 5 };
using var service = new ReportExportService(reports);

Console.WriteLine("=== Report Export Service Started ===\n");
Console.WriteLine($"Queue: {reports.Length} reports pending\n");

var workTask = service.StartAsync();

await Task.Delay(6000);
Console.WriteLine("[MAIN] Requesting shutdown...\n");

await service.StopAsync();

await workTask;
Console.WriteLine("Service fully stopped.");

public class ReportExportService : IDisposable
{
    private readonly Queue<int> _reportQueue;
    private readonly CancellationTokenSource _cts;
    private int _completedCount;
    private int _interruptedCount;
    private int _currentReportId;

    public ReportExportService(IEnumerable<int> reports)
    {
        _reportQueue = new Queue<int>(reports);
        _cts = new CancellationTokenSource();
        RegisterCleanupHandlers();
    }

    private void RegisterCleanupHandlers()
    {
        _cts.Token.Register(() =>
        {
            Console.WriteLine("[CLEANUP] Closing DB connection...");
        });

        _cts.Token.Register(() =>
        {
            int remaining = _reportQueue.Count;
            Console.WriteLine($"[CLEANUP] Saving queue state ({remaining} reports remaining)...");
        });

        _cts.Token.Register(() =>
        {
            Console.WriteLine("[CLEANUP] Admin notified.");
        });
    }

    public async Task StartAsync()
    {
        while (_reportQueue.Count > 0)
        {
            if (_cts.Token.IsCancellationRequested)
                break;

            _currentReportId = _reportQueue.Dequeue();

            try
            {
                await GenerateReportWithTimeout(_currentReportId, 10);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[Report #{_currentReportId}] Interrupted by shutdown.");
                Interlocked.Increment(ref _interruptedCount);
            }
        }
    }

    public async Task StopAsync()
    {
        Console.WriteLine("[SHUTDOWN] Stop requested. Finishing current report...");
        _cts.Cancel();

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await Task.Delay(-1, timeoutCts.Token);
        }
        catch (OperationCanceledException) { }

        Console.WriteLine($"\n=== Shutdown Complete ===");
        Console.WriteLine($"Completed: {_completedCount} | Interrupted: {_interruptedCount} | Remaining in queue: {_reportQueue.Count}");
    }

    private async Task GenerateReportWithTimeout(int reportId, int timeoutSeconds)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _cts.Token,
            new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)).Token
        );

        Console.WriteLine($"\n[Report #{reportId}] Started...");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await ProcessStages(reportId, linkedCts.Token);
            sw.Stop();
            Interlocked.Increment(ref _completedCount);
            Console.WriteLine($"[Report #{reportId}] Generated successfully in {sw.Elapsed.TotalSeconds:F1}s");
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested && !_cts.IsCancellationRequested)
        {
            Console.WriteLine($"[Report #{reportId}] TIMEOUT after {timeoutSeconds}s");
            Interlocked.Increment(ref _interruptedCount);
        }
    }

    private async Task ProcessStages(int reportId, CancellationToken ct)
    {
        var stages = new[] { "Querying DB", "Calculating aggregates", "Rendering PDF", "Uploading to S3" };
        var rng = new Random(reportId);

        foreach (string stage in stages)
        {
            ct.ThrowIfCancellationRequested();
            Console.WriteLine($"[Report #{reportId}]   {stage}...");

            int duration = rng.Next(500, 1500);
            await Task.Delay(duration, ct);
        }
    }

    public void Dispose()
    {
        _cts.Dispose();
    }
}
```

## Ключевые моменты

1. **CancellationTokenSource** — создаёт токен. `Cancel()` активирует его. Все подписчики (через токен) узнают об отмене.

2. **ThrowIfCancellationRequested()** — бросает `OperationCanceledException`, если отмена запрошена. Нужно вызывать на каждом логическом шаге, чтобы вовремя прервать работу.

3. **Linked Tokens**: `CreateLinkedTokenSource(ct1, ct2)` создаёт токен, который активируется при отмене ЛЮБОГО из исходных. Идеально для: основной shutdown + таймаут операции.

4. **await Task.Delay(..., ct)**: передача токена в Delay позволяет прервать ожидание без ожидания полного таймаута. Если ct отменён — Delay сразу бросает `OperationCanceledException`.

5. **Register**: регистрирует callback, который вызывается при отмене. Используется для cleanup-а: закрыть соединения, сохранить состояние, уведомить.

6. **Graceful shutdown**: при получении сигнала остановки — доделать текущую задачу (не бросать), но не начинать новые.

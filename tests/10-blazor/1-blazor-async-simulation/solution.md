# Решение: Эмуляция Blazor Server Dispatcher

```csharp
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

Console.WriteLine("=== Blazor Server Dispatcher Started ===\n");

using var dispatcher = new BlazorDispatcher();
var dashboard = new MetricsDashboard();

// Scenario 1: Cross-thread error
Console.WriteLine("--- Scenario 1: Cross-thread access (WITHOUT InvokeAsync) ---");
Task.Run(() => dashboard.UpdateWithoutInvokeAsync(78.3, 64.1)).Wait();
Console.WriteLine();

// Scenario 2: Correct InvokeAsync
Console.WriteLine("--- Scenario 2: Correct update (WITH InvokeAsync) ---");
await Task.Run(() => dashboard.UpdateWithInvokeAsync(dispatcher, 78.3, 64.1, 1423));
Console.WriteLine();

// Scenario 3: Timer monitoring
Console.WriteLine("--- Scenario 3: Timer (periodic updates) ---");
dashboard.StartMonitoring(dispatcher);
await Task.Delay(3500);
dashboard.StopMonitoring();
Console.WriteLine();

// Scenario 4: Lifecycle
Console.WriteLine("--- Scenario 4: OnInitializedAsync timing ---");
await dashboard.DemonstrateLifecycle(dispatcher);
Console.WriteLine();

// Scenario 5: JSInterop
Console.WriteLine("--- Scenario 5: JSInterop emulation ---");
await dashboard.DemonstrateJsInterop(dispatcher);
Console.WriteLine();

// Comparison
Console.WriteLine("--- Blazor Server vs WASM ---");
dispatcher.Stop();

public class BlazorDispatcher : SynchronizationContext
{
    private readonly BlockingCollection<(SendOrPostCallback, object?)> _queue = new();
    private readonly Thread _uiThread;
    private volatile bool _running = true;
    public int UiThreadId => _uiThread.ManagedThreadId;

    public BlazorDispatcher()
    {
        _uiThread = new Thread(RunLoop) { IsBackground = true, Name = "Blazor-Dispatcher" };
        _uiThread.Start();
        Console.WriteLine($"Dispatcher running on thread {UiThreadId}.\n");
    }

    private void RunLoop()
    {
        SetSynchronizationContext(this);
        while (_running)
        {
            if (_queue.TryTake(out var item, 50))
            {
                try { item.Item1(item.Item2); }
                catch (Exception ex) { Console.WriteLine($"[Dispatcher] Exception: {ex.Message}"); }
            }
        }
    }

    public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));
    public override void Send(SendOrPostCallback d, object? state)
    {
        using var mre = new ManualResetEventSlim();
        _queue.Add((s => { try { d(s); } finally { mre.Set(); } }, state));
        mre.Wait();
    }

    public Task InvokeAsync(Action action)
    {
        var tcs = new TaskCompletionSource();
        Post(_ => { try { action(); tcs.SetResult(); } catch (Exception ex) { tcs.SetException(ex); } }, null);
        return tcs.Task;
    }

    public Task InvokeAsync(Func<Task> asyncAction)
    {
        var tcs = new TaskCompletionSource();
        Post(async _ =>
        {
            try { await asyncAction(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        }, null);
        return tcs.Task;
    }

    public void Stop() { _running = false; _uiThread.Join(2000); }
}

public record struct MetricData(double Cpu, double Memory, int RequestsPerSec);

public class MetricsDashboard
{
    public double Cpu { get; private set; }
    public double Memory { get; private set; }
    public int RequestsPerSec { get; private set; }
    private Timer? _timer;

    public void UpdateWithoutInvokeAsync(double cpu, double memory)
    {
        var ctx = SynchronizationContext.Current as BlazorDispatcher;
        if (ctx != null && Environment.CurrentManagedThreadId != ctx.UiThreadId)
        {
            Console.WriteLine($"[Timer Thread {Environment.CurrentManagedThreadId}] Trying to update dashboard directly...");
            Console.WriteLine($"ERROR: Cross-thread access detected! Current thread ({Environment.CurrentManagedThreadId}) != Dispatcher thread ({ctx.UiThreadId}).");
            Console.WriteLine("Use InvokeAsync to marshal to the Dispatcher.");
            return;
        }

        Cpu = cpu;
        Memory = memory;
        Render();
    }

    public async Task UpdateWithInvokeAsync(BlazorDispatcher dispatcher, double cpu, double memory, int rps)
    {
        Console.WriteLine($"[Timer Thread {Environment.CurrentManagedThreadId}] Received metric data, calling InvokeAsync...");
        await dispatcher.InvokeAsync(() =>
        {
            Cpu = cpu;
            Memory = memory;
            RequestsPerSec = rps;
            Console.WriteLine($"[Dispatcher Thread {Environment.CurrentManagedThreadId}] Updated CPU: {Cpu}%");
            Console.WriteLine($"[Dispatcher Thread {Environment.CurrentManagedThreadId}] Updated Memory: {Memory}%");
            Render();
        });
    }

    public void Render()
    {
        Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] === DASHBOARD RENDER ===");
        Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}]   CPU: {Cpu}% | Memory: {Memory}% | Requests: {RequestsPerSec}/s\n");
    }

    public void StartMonitoring(BlazorDispatcher dispatcher)
    {
        var rng = new Random();
        _timer = new Timer(async _ =>
        {
            var data = new MetricData(
                Math.Round(70 + rng.NextDouble() * 20, 1),
                Math.Round(60 + rng.NextDouble() * 15, 1),
                rng.Next(1000, 2000)
            );

            Console.WriteLine($"[Timer Thread {Environment.CurrentManagedThreadId}] Tick → InvokeAsync");
            await dispatcher.InvokeAsync(() =>
            {
                Cpu = data.Cpu;
                Memory = data.Memory;
                RequestsPerSec = data.RequestsPerSec;
                Console.WriteLine($"[Dispatcher Thread {Environment.CurrentManagedThreadId}] Tick: CPU {Cpu}%, Mem {Memory}%, Req {RequestsPerSec}/s\n");
            });
        }, null, 500, 1000);

        Thread.Sleep(100);
    }

    public void StopMonitoring() => _timer?.Dispose();

    public async Task DemonstrateLifecycle(BlazorDispatcher dispatcher)
    {
        await dispatcher.InvokeAsync(async () =>
        {
            Console.WriteLine($"[Dispatcher Thread {Environment.CurrentManagedThreadId}] === RENDER #1: Loading... ===");

            // OnInitializedAsync — data loads asynchronously
            await Task.Delay(200);
            Console.WriteLine($"[Dispatcher Thread {Environment.CurrentManagedThreadId}] Data loaded! Status: Healthy");

            Cpu = 45;
            Memory = 72;
            RequestsPerSec = 1234;

            Console.WriteLine($"[Dispatcher Thread {Environment.CurrentManagedThreadId}] === RENDER #2 ===");
            Render();
            Console.WriteLine("NOTE: First render happens BEFORE data loads (like Blazor Server).");
        });
    }

    public async Task DemonstrateJsInterop(BlazorDispatcher dispatcher)
    {
        await dispatcher.InvokeAsync(async () =>
        {
            Console.WriteLine($"[Dispatcher Thread {Environment.CurrentManagedThreadId}] Calling JS: getScreenWidth...");
            int width = await JsEmulator.InvokeAsync<int>("getScreenWidth");
            Console.WriteLine($"[Dispatcher Thread {Environment.CurrentManagedThreadId}] JS call returned: {width}px");
            Console.WriteLine("JSInterop is always async — crossing JS/.NET boundary.");
        });
    }
}

public static class JsEmulator
{
    public static async Task<T> InvokeAsync<T>(string functionName)
    {
        Console.WriteLine($"[JS Emulator] Executing in JS runtime...");
        await Task.Delay(100);
        Console.WriteLine($"[JS Emulator] {functionName} = 1920px");
        return (T)(object)1920;
    }
}
```

## Ключевые моменты

1. **Blazor Server Dispatcher**: каждый circuit имеет свой Dispatcher (SynchronizationContext). Все обновления UI должны проходить через него. Background-потоки (Timer, Message Bus) используют `InvokeAsync` для маршалинга.

2. **InvokeAsync**: если поток уже на Dispatcher — выполняется синхронно. Если на другом потоке — Post в очередь Dispatcher. Без `InvokeAsync` — `InvalidOperationException` (cross-thread access).

3. **Blazor WASM**: работает в single-threaded среде браузера. `Thread.Sleep` блокирует весь UI. `Task.Delay` работает через JS timers. `InvokeAsync` не нужен (всё на одном потоке). `Parallel.For` выполняется последовательно.

4. **OnInitializedAsync**: первый рендер происходит ДО завершения асинхронной инициализации (Blazor Server). После загрузки данных — автоматический повторный рендер.

5. **JSInterop**: `IJSRuntime.InvokeAsync<T>()` ВСЕГДА async, потому что пересекает границу JS/.NET. Даже из Dispatcher-потока.

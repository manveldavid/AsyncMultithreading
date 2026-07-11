namespace AsyncMultithreadDemo.Demos;

public static class Demo12_BlazorAsyncSimulation
{
    public static async Task Run()
    {
        Console.WriteLine("=== Blazor async patterns simulation ===\n");

        Console.WriteLine("  In Blazor Server, there is a Dispatcher (SynchronizationContext-like).");
        Console.WriteLine("  All UI updates must go through it.");
        Console.WriteLine("  Background threads MUST use InvokeAsync to touch component state.\n");

        Console.WriteLine("--- 1. Simulating Blazor Dispatcher ---\n");

        var dispatcher = new FakeDispatcher();

        Console.WriteLine("  Without InvokeAsync (race condition):");

        int uiState = 0;
        var cts = new CancellationTokenSource();

        var backgroundWork = Task.Run(async () =>
        {
            for (int i = 0; i < 5; i++)
            {
                await Task.Delay(50);
                uiState = i;
                Console.WriteLine($"    [Background] Set uiState={i} (thread={Environment.CurrentManagedThreadId})");
            }
        });

        var uiReader = Task.Run(async () =>
        {
            for (int i = 0; i < 5; i++)
            {
                await Task.Delay(60);
                Console.WriteLine($"    [UI] Read uiState={uiState} (thread={Environment.CurrentManagedThreadId})");
            }
        });

        await Task.WhenAll(backgroundWork, uiReader);

        Console.WriteLine("\n  With InvokeAsync (proper ordering):");

        uiState = 0;
        var updates = new List<int>();

        backgroundWork = Task.Run(async () =>
        {
            for (int i = 0; i < 5; i++)
            {
                await Task.Delay(50);
                int newVal = i;
                await dispatcher.InvokeAsync(() =>
                {
                    uiState = newVal;
                    updates.Add(uiState);
                    Console.WriteLine($"    [UI Thread] Set uiState={newVal} via InvokeAsync");
                });
            }
        });

        await backgroundWork;
        await dispatcher.DrainAsync();

        Console.WriteLine($"  Final uiState: {uiState}");
        Console.WriteLine($"  Updates applied in order: [{string.Join(", ", updates)}]");

        Console.WriteLine("\n--- 2. Simulating JSInterop ---\n");

        Console.WriteLine("  JSInterop is always async (crossing process boundary).");
        Console.WriteLine("  IJSRuntime.InvokeAsync<T>('functionName', args)");
        Console.WriteLine();

        var jsRuntime = new FakeJSRuntime();

        Console.WriteLine("  Calling JS: alert('Hello from .NET')");
        await jsRuntime.InvokeVoidAsync("alert", "Hello from .NET");

        Console.WriteLine("  Calling JS: prompt('Enter name:')");
        var name = await jsRuntime.InvokeAsync<string>("prompt", "Enter name:");
        Console.WriteLine($"  JS returned: '{name}'");

        Console.WriteLine("\n  [JSInvokable] — C# method callable from JS:");
        var objRef = new DotNetObjectReference<FakeComponent>(new FakeComponent());
        Console.WriteLine("  JS calls: DotNet.invokeMethodAsync('Assembly', 'GetServerTime')");
        var result = objRef.InvokeMethod("GetServerTime");
        Console.WriteLine($"  C# returned: {result}");

        Console.WriteLine("\n--- 3. Blazor lifecycle ---\n");

        Console.WriteLine("  OnInitializedAsync:");
        Console.WriteLine("    In Blazor Server, first render happens BEFORE this completes.");
        Console.WriteLine("    UI shows loading state, then updates when async work finishes.");
        Console.WriteLine();

        var component = new FakeComponent();
        Console.WriteLine("  Calling OnInitializedAsync...");
        await component.OnInitializedAsync();
        Console.WriteLine($"  Component data loaded: {component.Data}");

        Console.WriteLine("\n--- 4. StateHasChanged ---\n");
        Console.WriteLine("  StateHasChanged tells Blazor to re-render the component.");
        Console.WriteLine("  Automatically called after lifecycle methods and event callbacks.");
        Console.WriteLine("  Must be called manually when:");
        Console.WriteLine("    • Background work updates state");
        Console.WriteLine("    • Using InvokeAsync from a different thread");
        Console.WriteLine("    • External events (timers, SignalR) change state");
    }
}

internal sealed class FakeDispatcher
{
    private readonly Queue<Func<Task>> _queue = new();
    private readonly object _lock = new();

    public Task InvokeAsync(Action work)
    {
        var tcs = new TaskCompletionSource();
        lock (_lock)
        {
            _queue.Enqueue(() =>
            {
                try
                {
                    work();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
                return Task.CompletedTask;
            });
        }
        return tcs.Task;
    }

    public async Task DrainAsync()
    {
        while (true)
        {
            Func<Task>? item;
            lock (_lock)
            {
                if (_queue.Count == 0) break;
                item = _queue.Dequeue();
            }
            await item();
        }
    }
}

internal sealed class FakeJSRuntime
{
    public Task InvokeVoidAsync(string identifier, params object[] args)
    {
        Console.WriteLine($"    [JS] {identifier}({string.Join(", ", args.Select(a => $"'{a}'"))})");
        return Task.CompletedTask;
    }

    public Task<T> InvokeAsync<T>(string identifier, params object[] args)
    {
        Console.WriteLine($"    [JS] {identifier}({string.Join(", ", args.Select(a => $"'{a}'"))})");

        object result = identifier switch
        {
            "prompt" => "UserInput",
            "confirm" => true,
            _ => default!
        };

        return Task.FromResult((T)result);
    }
}

internal sealed class FakeComponent
{
    public string Data { get; private set; } = "";

    public async Task OnInitializedAsync()
    {
        Console.WriteLine("    [Component] OnInitializedAsync started");
        Console.WriteLine("    [Component] First render happens NOW (with empty data)");
        await Task.Delay(100);
        Data = "Loaded from async source";
        Console.WriteLine("    [Component] Async work done, StateHasChanged() called");
    }

    public string GetServerTime() => DateTime.Now.ToString("HH:mm:ss.fff");
}

internal sealed class DotNetObjectReference<T> where T : class
{
    public T Value { get; }

    public DotNetObjectReference(T value) => Value = value;

    public object? InvokeMethod(string methodName)
    {
        var method = typeof(T).GetMethod(methodName);
        return method?.Invoke(Value, null);
    }
}

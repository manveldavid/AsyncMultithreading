namespace AsyncMultithreadDemo.Demos;

public static class Demo03_TaskDeepDive
{
    public static async Task Run()
    {
        Console.WriteLine("=== 1. Task creation methods ===\n");

        var fromResult = Task.FromResult(42);
        Console.WriteLine($"  Task.FromResult(42): Result={fromResult.Result}, Status={fromResult.Status}");

        var completed = Task.CompletedTask;
        Console.WriteLine($"  Task.CompletedTask: Status={completed.Status}");

        var fromException = Task.FromException<int>(new InvalidOperationException("oops"));
        Console.WriteLine($"  Task.FromException: Status={fromException.Status}, IsFaulted={fromException.IsFaulted}");

        Console.WriteLine("\n=== 2. Task.Run vs Task.Factory.StartNew vs new Task ===\n");

        Console.WriteLine("  Task.Run — unwraps inner Task<Task> automatically (preferred)");
        Task<int> runTask = Task.Run(() =>
        {
            Thread.Sleep(50);
            return Environment.CurrentManagedThreadId;
        });
        Console.WriteLine($"  Task.Run result: thread={runTask.Result}");

        Console.WriteLine("  Task.Factory.StartNew — does NOT unwrap Task<Task>");
        Task<Task<int>> factoryTask = Task.Factory.StartNew(() => Task.FromResult(Environment.CurrentManagedThreadId));
        Console.WriteLine($"  Factory task type: {factoryTask.GetType().Name} (nested!)");
        Console.WriteLine($"  Factory task result (unwrapped): {factoryTask.Unwrap().Result}");

        Console.WriteLine("  new Task() — cold task, must call .Start()");
        var coldTask = new Task<int>(() =>
        {
            Thread.Sleep(50);
            return Environment.CurrentManagedThreadId;
        });
        Console.WriteLine($"  Cold task status before Start: {coldTask.Status}");
        coldTask.Start();
        Console.WriteLine($"  Cold task result: {coldTask.Result}");

        Console.WriteLine("\n=== 3. CancellationToken ===\n");

        using var cts = new CancellationTokenSource();

        var workTask = LongRunningWorkAsync(cts.Token);

        await Task.Delay(300);
        Console.WriteLine("  Requesting cancellation...");
        cts.Cancel();

        try
        {
            await workTask;
        }
        catch (OperationCanceledException ex)
        {
            Console.WriteLine($"  Caught: {ex.GetType().Name} — {ex.Message}");
        }

        Console.WriteLine("\n=== 4. Linked CancellationToken ===\n");

        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts1.Token, cts2.Token);

        var linkedTask = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, linked.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("  Linked token cancelled");
                throw;
            }
        }, linked.Token);

        await Task.Delay(100);
        cts2.Cancel();
        Console.WriteLine("  Cancelled cts2 (linked) — linked is also cancelled");

        try { await linkedTask; } catch (OperationCanceledException) { }

        Console.WriteLine("\n=== 5. WhenAny as timeout ===\n");

        var slowTask = Task.Run(async () =>
        {
            await Task.Delay(5000);
            return "done";
        });

        var timeoutTask = Task.Delay(500);
        var completedTask = await Task.WhenAny(slowTask, timeoutTask);

        if (completedTask == timeoutTask)
            Console.WriteLine("  Timeout! slowTask did not complete in 500ms");
        else
            Console.WriteLine($"  Completed: {slowTask.Result}");

        Console.WriteLine("\n=== 6. Error handling ===\n");

        var faultedTask = Task.Run(() => { throw new DivideByZeroException("boom"); return 0; });

        Console.WriteLine("  .Result — wraps in AggregateException:");
        try
        {
            _ = faultedTask.Result;
        }
        catch (AggregateException ex)
        {
            Console.WriteLine($"    AggregateException with {ex.InnerExceptions.Count} inner:");
            foreach (var inner in ex.InnerExceptions)
                Console.WriteLine($"      {inner.GetType().Name}: {inner.Message}");
        }

        Console.WriteLine("\n  await — unwraps first exception:");
        var faultedTask2 = Task.Run(() => { throw new DivideByZeroException("boom"); return 0; });
        try
        {
            await faultedTask2;
        }
        catch (DivideByZeroException ex)
        {
            Console.WriteLine($"    {ex.GetType().Name}: {ex.Message} (unwrapped!)");
        }
    }

    private static async Task LongRunningWorkAsync(CancellationToken ct)
    {
        Console.WriteLine("  Long-running work started...");
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(100, ct);
            Console.WriteLine($"    Step {i + 1}/10");
            ct.ThrowIfCancellationRequested();
        }
        Console.WriteLine("  Work completed");
    }
}

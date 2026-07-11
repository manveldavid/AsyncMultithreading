namespace AsyncMultithreadDemo.Demos;

public static class Demo10_ThreadLocalAsyncLocal
{
    private static readonly ThreadLocal<string> _threadLocal = new(() => $"default-{Environment.CurrentManagedThreadId}");
    private static readonly AsyncLocal<string> _asyncLocal = new();

    public static async Task Run()
    {
        Console.WriteLine("=== ThreadLocal vs AsyncLocal ===\n");

        Console.WriteLine("--- 1. ThreadLocal<T>: data per thread ---\n");

        _threadLocal.Value = "main-thread-value";
        Console.WriteLine($"  Main thread: {_threadLocal.Value}");

        await Task.Run(() =>
        {
            Console.WriteLine($"  ThreadPool thread: {_threadLocal.Value}");
            _threadLocal.Value = "pool-thread-value";
            Console.WriteLine($"  After set: {_threadLocal.Value}");
        });

        Console.WriteLine($"  Main thread (after pool): {_threadLocal.Value}");

        Console.WriteLine("\n  CAVEAT: ThreadPool reuses threads!");
        Console.WriteLine("  ThreadLocal value can leak to unrelated work.\n");

        Console.WriteLine("--- 2. ThreadLocal leak in ThreadPool ---\n");

        var leakLocal = new ThreadLocal<string>(() => "uninitialized");

        var t1 = Task.Run(() =>
        {
            leakLocal.Value = "task-1-set-this";
            Console.WriteLine($"  Task 1 (thread {Environment.CurrentManagedThreadId}): {leakLocal.Value}");
        });
        await t1;

        var t2 = Task.Run(() =>
        {
            Console.WriteLine($"  Task 2 (thread {Environment.CurrentManagedThreadId}): {leakLocal.Value}");
            Console.WriteLine("  ↑ Leaked value from Task 1 if same thread reused!");
        });
        await t2;

        Console.WriteLine("\n--- 3. AsyncLocal<T>: flows through async context ---\n");

        _asyncLocal.Value = "root-value";
        Console.WriteLine($"  Root: {_asyncLocal.Value}");

        await Level1Async();

        Console.WriteLine($"  Root (after async chain): {_asyncLocal.Value}");
        Console.WriteLine("  AsyncLocal value flowed through await, but changes in child don't propagate up.\n");

        Console.WriteLine("--- 4. AsyncLocal: practical use — Correlation ID ---\n");

        var correlationId = new AsyncLocal<string>();
        correlationId.Value = "req-abc-123";

        await ProcessRequestAsync(correlationId);

        Console.WriteLine("\n--- 5. Comparison ---\n");
        Console.WriteLine("  ThreadLocal<T>:");
        Console.WriteLine("    ✓ Data isolated per OS thread");
        Console.WriteLine("    ✗ Leaks in ThreadPool");
        Console.WriteLine("    ✗ Does not flow across await");
        Console.WriteLine("    Use for: per-thread caches, random generators");
        Console.WriteLine();
        Console.WriteLine("  AsyncLocal<T>:");
        Console.WriteLine("    ✓ Flows through async/await (ExecutionContext)");
        Console.WriteLine("    ✓ Changes in child don't affect parent (copy-on-write)");
        Console.WriteLine("    ✓ Thread-safe in async context");
        Console.WriteLine("    Use for: correlation IDs, tenant context, transaction scope");
    }

    private static async Task Level1Async()
    {
        Console.WriteLine($"    Level 1: {_asyncLocal.Value}");
        _asyncLocal.Value = "level-1-value";
        await Level2Async();
        Console.WriteLine($"    Level 1 (after Level 2): {_asyncLocal.Value}");
    }

    private static async Task Level2Async()
    {
        Console.WriteLine($"      Level 2: {_asyncLocal.Value}");
        _asyncLocal.Value = "level-2-value";
        await Task.Delay(50);
        Console.WriteLine($"      Level 2 (after await): {_asyncLocal.Value}");
    }

    private static async Task ProcessRequestAsync(AsyncLocal<string> correlationId)
    {
        Console.WriteLine($"  [Handler] CorrelationId: {correlationId.Value}");
        await ServiceCallAsync(correlationId);
    }

    private static async Task ServiceCallAsync(AsyncLocal<string> correlationId)
    {
        Console.WriteLine($"    [Service] CorrelationId: {correlationId.Value}");
        await Task.Delay(50);
        Console.WriteLine($"    [Service] After await: {correlationId.Value}");
    }
}

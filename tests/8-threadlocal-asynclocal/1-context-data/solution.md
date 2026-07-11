# Решение: Система логирования — ThreadLocal vs AsyncLocal

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

Console.WriteLine("=== ThreadLocal Leak in ThreadPool ===\n");
LoggingContext.DemonstrateThreadLocalLeakInThreadPool();

Console.WriteLine("\n=== ThreadLocal Loss Across Await ===\n");
await LoggingContext.DemonstrateThreadLocalLossAcrossAwait();

Console.WriteLine("\n=== AsyncLocal Flow ===\n");
await LoggingContext.DemonstrateAsyncLocalFlow();

Console.WriteLine("\n=== Copy-on-Write Semantics ===\n");
await LoggingContext.DemonstrateCopyOnWrite();

Console.WriteLine("\n=== Full Request Pipeline ===\n");
await LoggingContext.SimulateRequestPipeline();

public static class LoggingContext
{
    private static readonly ThreadLocal<string> _threadLocal = new(() => "unset");
    private static readonly AsyncLocal<string> _asyncLocal = new();

    public static void DemonstrateThreadLocalLeakInThreadPool()
    {
        var t1 = Task.Run(() =>
        {
            _threadLocal.Value = "request-111";
            Console.WriteLine($"[Task 1, Thread {Environment.CurrentManagedThreadId}] Set ThreadLocal to \"{_threadLocal.Value}\"");
        });
        t1.Wait();

        for (int i = 0; i < 5; i++)
        {
            var t2 = Task.Run(() =>
            {
                Console.WriteLine($"[Task 2, Thread {Environment.CurrentManagedThreadId}] ThreadLocal = \"{_threadLocal.Value}\"");
            });
            t2.Wait();
        }
        Console.WriteLine("WARNING: ThreadLocal values leak when ThreadPool reuses threads!");
    }

    public static async Task DemonstrateThreadLocalLossAcrossAwait()
    {
        _threadLocal.Value = "before-await";
        Console.WriteLine($"[Before await, Thread {Environment.CurrentManagedThreadId}] ThreadLocal = \"{_threadLocal.Value}\"");
        await Task.Delay(50);
        Console.WriteLine($"[After await, Thread {Environment.CurrentManagedThreadId}]  ThreadLocal = \"{_threadLocal.Value}\" ← LOST! Thread changed.");
    }

    public static async Task DemonstrateAsyncLocalFlow()
    {
        _asyncLocal.Value = "request-999";
        Console.WriteLine($"[Before await, Thread {Environment.CurrentManagedThreadId}] AsyncLocal = \"{_asyncLocal.Value}\"");

        await Level1();

        Console.WriteLine($"[After all awaits, Thread {Environment.CurrentManagedThreadId}]       AsyncLocal = \"{_asyncLocal.Value}\" ← Persisted!");
    }

    private static async Task Level1()
    {
        Console.WriteLine($"[Level 1, Thread {Environment.CurrentManagedThreadId}]      AsyncLocal = \"{_asyncLocal.Value}\" ← FLOWED correctly!");
        _asyncLocal.Value = "level-1-changed";
        await Level2();
    }

    private static async Task Level2()
    {
        Console.WriteLine($"[Level 2, Thread {Environment.CurrentManagedThreadId}]      AsyncLocal = \"{_asyncLocal.Value}\" ← Still there!");
        await Task.Delay(30);
    }

    public static async Task DemonstrateCopyOnWrite()
    {
        _asyncLocal.Value = "parent";
        Console.WriteLine($"[Parent, Thread {Environment.CurrentManagedThreadId}] Set AsyncLocal = \"{_asyncLocal.Value}\"");

        await Child();

        Console.WriteLine($"[Parent, Thread {Environment.CurrentManagedThreadId}] Read AsyncLocal = \"{_asyncLocal.Value}\" ← NOT changed by child!");
    }

    private static async Task Child()
    {
        Console.WriteLine($"  [Child, Thread {Environment.CurrentManagedThreadId}]  Read AsyncLocal = \"{_asyncLocal.Value}\" (flowed from parent)");
        _asyncLocal.Value = "child";
        Console.WriteLine($"  [Child, Thread {Environment.CurrentManagedThreadId}]  Set AsyncLocal = \"{_asyncLocal.Value}\"");
        await Task.Delay(30);
        Console.WriteLine($"  [Child, Thread {Environment.CurrentManagedThreadId}]  Read AsyncLocal = \"{_asyncLocal.Value}\"");
    }

    public static async Task SimulateRequestPipeline()
    {
        string correlationId = Guid.NewGuid().ToString();
        _asyncLocal.Value = correlationId;

        await AuthMiddleware();
        await RateLimiterMiddleware();
        await OrderController();
        await OrderRepository();
        await Database();

        Console.WriteLine("Request pipeline complete — CorrelationId preserved through all awaits!");
    }

    private static async Task AuthMiddleware()
    {
        Console.WriteLine($"[{_asyncLocal.Value}] [AuthMiddleware]     Validating token...");
        await Task.Delay(20);
    }

    private static async Task RateLimiterMiddleware()
    {
        Console.WriteLine($"[{_asyncLocal.Value}] [RateLimiter]        Checking limits...");
        await Task.Delay(15);
    }

    private static async Task OrderController()
    {
        Console.WriteLine($"[{_asyncLocal.Value}] [OrderController]    Processing order #12345");
        await Task.Delay(25);
    }

    private static async Task OrderRepository()
    {
        Console.WriteLine($"[{_asyncLocal.Value}] [OrderRepository]    Querying database...");
        await Task.Delay(30);
    }

    private static async Task Database()
    {
        Console.WriteLine($"[{_asyncLocal.Value}] [Database]           SELECT * FROM orders...");
        await Task.Delay(20);
    }
}
```

## Ключевые моменты

1. **ThreadLocal<T>**: значение привязано к потоку ОС. При await поток меняется → значение теряется. В ThreadPool потоки переиспользуются → значение «протекает» между задачами.

2. **AsyncLocal<T>**: значение течёт через `ExecutionContext` — копируется при каждом `await`. Всегда доступно в async-цепочке, независимо от переключения потоков.

3. **Copy-on-Write**: дочерний метод получает КОПИЮ значения (flowing), а не ссылку. Изменения в дочернем НЕ влияют на родителя.

4. **Сценарии**: `AsyncLocal` идеален для Correlation ID, Tenant ID, transaction context, культуры, пользователя. `ThreadLocal` — для per-thread кэшей (Random), где не нужен async-flow.

5. **ExecutionContext**: включает SecurityContext, Thread.CurrentPrincipal, CultureInfo и AsyncLocal-ы. Копируется при await, передаётся через `Task.Run` (если не `UnsafeQueueUserWorkItem`).

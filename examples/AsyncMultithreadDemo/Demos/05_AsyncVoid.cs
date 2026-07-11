namespace AsyncMultithreadDemo.Demos;

public static class Demo05_AsyncVoid
{
    public static async Task Run()
    {
        Console.WriteLine("=== async void: why it's dangerous ===\n");

        Console.WriteLine("  async void exists for backward compatibility with event handlers.");
        Console.WriteLine("  The caller CANNOT await it. Exceptions are NOT caught by try/catch around the call.");
        Console.WriteLine("  Exceptions go directly to SynchronizationContext or AppDomain.UnhandledException.\n");

        Console.WriteLine("--- 1. async Task — exception is catchable ---");
        try
        {
            await ThrowAsyncTask();
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"  Caught: {ex.Message}");
        }

        Console.WriteLine("\n--- 2. async void — exception escapes try/catch ---");

        var tcs = new TaskCompletionSource();
        void handler(object? sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)e.ExceptionObject;
            Console.WriteLine($"  AppDomain caught: {ex.GetType().Name}: {ex.Message}");
            tcs.TrySetResult();
        }

        AppDomain.CurrentDomain.UnhandledException += handler;

        try
        {
            ThrowAsyncVoid();
            Console.WriteLine("  This line executes — async void returned immediately");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  try/catch caught: {ex.Message}");
        }

        await Task.WhenAny(tcs.Task, Task.Delay(2000));
        AppDomain.CurrentDomain.UnhandledException -= handler;

        Console.WriteLine("\n--- 3. Comparison ---");
        Console.WriteLine("  async Task:   caller can await, exception goes to Task, catchable");
        Console.WriteLine("  async void:   caller CANNOT await, exception crashes the process");
        Console.WriteLine("  Rule: ONLY use async void for event handlers. Everything else — async Task.");

        Console.WriteLine("\n--- 4. Fire-and-forget pattern (correct way) ---");
        Console.WriteLine("  Instead of async void, use:");
        Console.WriteLine("    _ = DoWorkAsync();                           // discard");
        Console.WriteLine("    _ = Task.Run(() => DoWorkAsync());           // background");
        Console.WriteLine("    var __ = DoWorkAsync().ContinueWith(t => ...); // observe");
    }

    private static async Task ThrowAsyncTask()
    {
        await Task.Delay(50);
        throw new InvalidOperationException("from async Task");
    }

    private static async void ThrowAsyncVoid()
    {
        await Task.Delay(100);
        throw new InvalidOperationException("from async void — UNOBSERVED!");
    }
}

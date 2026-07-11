using System.Diagnostics;

namespace AsyncMultithreadDemo.Demos;

public static class Demo02_AsyncAwait
{
    public static async Task Run()
    {
        Console.WriteLine("=== 1. Basic async/await ===\n");

        Console.WriteLine($"  Before await — Thread={Environment.CurrentManagedThreadId}");
        await SimulateWorkAsync("Task-1", 500);
        Console.WriteLine($"  After await  — Thread={Environment.CurrentManagedThreadId}");

        Console.WriteLine("\n=== 2. SynchronizationContext ===\n");

        var ctx = SynchronizationContext.Current;
        Console.WriteLine($"  Console SynchronizationContext: {(ctx is null ? "null (no context)" : ctx.GetType().Name)}");
        Console.WriteLine("  In UI (WinForms/WPF) — it's WindowsFormsSynchronizationContext / DispatcherSynchronizationContext");
        Console.WriteLine("  In ASP.NET (pre-Core) — it was AspNetSynchronizationContext");
        Console.WriteLine("  In Console / ASP.NET Core — null (ThreadPool)");

        Console.WriteLine("\n=== 3. ConfigureAwait(false) ===\n");

        Console.WriteLine("  Without ConfigureAwait(false):");
        Console.WriteLine($"    Before: Thread={Environment.CurrentManagedThreadId}");
        await Task.Delay(100);
        Console.WriteLine($"    After:  Thread={Environment.CurrentManagedThreadId}");

        Console.WriteLine("  With ConfigureAwait(false):");
        Console.WriteLine($"    Before: Thread={Environment.CurrentManagedThreadId}");
        await Task.Delay(100).ConfigureAwait(false);
        Console.WriteLine($"    After:  Thread={Environment.CurrentManagedThreadId}");

        Console.WriteLine("\n  ConfigureAwait(false) — do not capture SynchronizationContext");
        Console.WriteLine("  Use in libraries to avoid deadlocks and reduce overhead");

        Console.WriteLine("\n=== 4. Task.WhenAll — parallel async ===\n");

        var sw = Stopwatch.StartNew();

        var t1 = SimulateWorkAsync("Alpha", 300);
        var t2 = SimulateWorkAsync("Beta", 500);
        var t3 = SimulateWorkAsync("Gamma", 200);

        await Task.WhenAll(t1, t2, t3);

        sw.Stop();
        Console.WriteLine($"  All 3 tasks done in {sw.ElapsedMilliseconds}ms (max of 300,500,200 = ~500ms)");

        Console.WriteLine("\n=== 5. Task.WhenAll — sequential async ===\n");

        sw.Restart();

        await SimulateWorkAsync("Alpha", 300);
        await SimulateWorkAsync("Beta", 500);
        await SimulateWorkAsync("Gamma", 200);

        sw.Stop();
        Console.WriteLine($"  Sequential in {sw.ElapsedMilliseconds}ms (sum of 300+500+200 = ~1000ms)");
    }

    private static async Task SimulateWorkAsync(string name, int delayMs)
    {
        Console.WriteLine($"    [{name}] Start on Thread={Environment.CurrentManagedThreadId}");
        await Task.Delay(delayMs);
        Console.WriteLine($"    [{name}] Done  on Thread={Environment.CurrentManagedThreadId}");
    }
}

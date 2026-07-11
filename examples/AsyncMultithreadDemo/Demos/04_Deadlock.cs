namespace AsyncMultithreadDemo.Demos;

public static class Demo04_Deadlock
{
    public static async Task Run()
    {
        Console.WriteLine("=== Deadlock: how it happens ===\n");
        Console.WriteLine("  In UI frameworks (WPF, WinForms) and old ASP.NET,");
        Console.WriteLine("  SynchronizationContext has 1 thread.");
        Console.WriteLine("  .Result / .Wait() BLOCKS that thread, waiting for Task.");
        Console.WriteLine("  Task continuation NEEDS that thread to post back.");
        Console.WriteLine("  → Classic deadlock.\n");

        Console.WriteLine("  Simulating with custom SynchronizationContext (single-thread):\n");

        var singleThreadCtx = new SingleThreadSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(singleThreadCtx);

        Console.WriteLine("  --- BAD: .Result causes deadlock (will timeout) ---");
        try
        {
            var cts = new CancellationTokenSource(2000);
            var task = Task.Run(() =>
            {
                singleThreadCtx.Post(_ =>
                {
                    Thread.Sleep(100);
                }, null);
                return 42;
            });

            var timeoutTask = Task.Delay(2000, cts.Token);
            var completed = await Task.WhenAny(
                Task.Run(() => task.Result),
                timeoutTask
            );

            if (completed == timeoutTask)
                Console.WriteLine("  DEADLOCK DETECTED — timed out after 2s (as expected)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Exception: {ex.Message}");
        }

        Console.WriteLine("\n  --- FIX 1: ConfigureAwait(false) ---");
        {
            var result = await DoWorkWithConfigureAwaitAsync().ConfigureAwait(false);
            Console.WriteLine($"  Result: {result} (no deadlock)");
        }

        Console.WriteLine("\n  --- FIX 2: async all the way ---");
        {
            var result = await DoWorkAsync();
            Console.WriteLine($"  Result: {result} (no blocking, no deadlock)");
        }

        Console.WriteLine("\n  --- FIX 3: Task.Run wrapper (legacy hack) ---");
        {
            var result = Task.Run(() => DoWorkAsync()).GetAwaiter().GetResult();
            Console.WriteLine($"  Result: {result} (runs on ThreadPool, avoids SyncCtx)");
        }

        Console.WriteLine("\n  --- FIX 4: Two locks deadlock example ---");
        {
            var lockA = new object();
            var lockB = new object();

            var t1 = Task.Run(() =>
            {
                lock (lockA)
                {
                    Thread.Sleep(50);
                    lock (lockB)
                    {
                        Console.WriteLine("    T1: acquired both");
                    }
                }
            });

            var t2 = Task.Run(() =>
            {
                lock (lockB)
                {
                    Thread.Sleep(50);
                    lock (lockA)
                    {
                        Console.WriteLine("    T2: acquired both");
                    }
                }
            });

            var timeout = Task.Delay(2000);
            var done = await Task.WhenAny(Task.WhenAll(t1, t2), timeout);
            if (done == timeout)
                Console.WriteLine("    Lock deadlock detected — timed out");
            else
                Console.WriteLine("    Both locks acquired (lucky timing)");
        }

        SynchronizationContext.SetSynchronizationContext(null);
    }

    private static async Task<int> DoWorkAsync()
    {
        await Task.Delay(50);
        return 42;
    }

    private static async Task<int> DoWorkWithConfigureAwaitAsync()
    {
        await Task.Delay(50).ConfigureAwait(false);
        return 42;
    }
}

internal sealed class SingleThreadSynchronizationContext : SynchronizationContext
{
    private readonly Queue<KeyValuePair<SendOrPostCallback, object?>> _queue = new();
    private readonly object _lock = new();
    private readonly Thread _thread;

    public SingleThreadSynchronizationContext()
    {
        _thread = new Thread(ProcessQueue) { IsBackground = true };
        _thread.Start();
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        lock (_lock)
        {
            _queue.Enqueue(new(d, state));
            Monitor.Pulse(_lock);
        }
    }

    public override void Send(SendOrPostCallback d, object? state) => d(state);

    private void ProcessQueue()
    {
        while (true)
        {
            KeyValuePair<SendOrPostCallback, object?> item;
            lock (_lock)
            {
                while (_queue.Count == 0)
                    Monitor.Wait(_lock, 100);
                item = _queue.Dequeue();
            }
            item.Key(item.Value);
        }
    }
}

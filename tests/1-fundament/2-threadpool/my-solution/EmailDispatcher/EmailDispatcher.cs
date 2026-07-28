using System.Diagnostics;

namespace EmailDispatcher;

public class EmailDispatcher
{
    public Random Random { get; } = new();
    public HashSet<int> ThreadIdInUse { get; } = new();
    public void ShowThreadPoolStats(string label)
    {
        ThreadPool.GetMinThreads(out var minWorkerThreads, out _);
        ThreadPool.GetMaxThreads(out var maxWorkerThreads, out _);
        ThreadPool.GetAvailableThreads(out var availableWorkerThreads, out _);

        var actual = 0;
        lock (ThreadIdInUse)
        {
            actual += ThreadIdInUse.Count;
        }

        Console.WriteLine(
            $"""

            === ThreadPool Stats ({label}) ===
            Min worker threads: {minWorkerThreads}, Max: {maxWorkerThreads}
            Available: {availableWorkerThreads} | Busy: {maxWorkerThreads - availableWorkerThreads} | Actual threads used: {actual}

            """);

    }

    public void SendAllEmails(int emailCount, bool waiting = true)
    {
        var countDown = new CountdownEvent(emailCount);

        for (var id = 0; id < emailCount; id++)
        {
            var userId = 0 + id;
            ThreadPool.QueueUserWorkItem(state =>
            {
                var threadId = Environment.CurrentManagedThreadId;

                lock (ThreadIdInUse)
                {
                    if (!ThreadIdInUse.Contains(threadId))
                        ThreadIdInUse.Add(threadId);
                }

                var delay = 0;
                lock (Random)
                {
                    delay = Random.Next(50, 150);
                }

                Thread.Sleep(delay);
                Console.WriteLine($"[ThreadPool-{threadId.ToString().PadLeft(2, '0')}] Sending email to user_{userId.ToString().PadLeft(3, '0')}@company.com... Done");

                countDown.Signal();
            });
        }

        if (waiting)
        {
            countDown.Wait();
            countDown.Dispose();
            ShowThreadPoolStats("after");
        }

        lock (ThreadIdInUse)
        {
            ThreadIdInUse.Clear();
        }
    }

    public void SimulateStarvation(int blockCount)
    {
        Console.WriteLine($"""

            === STARVATION DEMO ===
            Blocking {blockCount} threads for 3000ms...

            """);

        for (var id = 0; id < blockCount; id++)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                var threadId = Environment.CurrentManagedThreadId;
                Console.WriteLine($"[ThreadPool-{threadId.ToString().PadLeft(2,'0')}] BLOCKED (starvation contributor)");
                Thread.Sleep(3000);
            });
        }

        Thread.Sleep(200);
        Console.WriteLine("\nLaunching 20 urgent emails...\n");
        Console.WriteLine("Urgent emails sent in first 2 seconds v\n");
        SendAllEmails(20,false);
        Thread.Sleep(2000);
        Console.WriteLine("\nUrgent emails sent in first 2 seconds ^\n");
    }

    public void ShowHillClimbingEffect(int taskCount)
    {
        Console.WriteLine($"""

            === HILL CLIMBING DEMO ===
            Blocking {taskCount} threads for 500ms...
            """);
        ShowThreadPoolStats("before hill climbing");

        var countDown = new CountdownEvent(taskCount);
        for (var id = 0; id < taskCount; id++)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                var threadId = Environment.CurrentManagedThreadId; 
                lock (ThreadIdInUse)
                {
                    if (!ThreadIdInUse.Contains(threadId))
                        ThreadIdInUse.Add(threadId);
                }
                Thread.Sleep(500);
                countDown.Signal();
            });
        }

        Thread.Sleep(1000);

        ShowThreadPoolStats("during hill climbing");
        countDown.Wait();
        countDown.Dispose();

        ShowThreadPoolStats("after hill climbing");

        lock (ThreadIdInUse)
        {
            ThreadIdInUse.Clear();
        }
    }

    public void CompareQueueUserWorkItemVsUnsafe(int taskCount)
    {
        Console.WriteLine($"""

            === COMPARE QueueUserWorkItem VS Unsafe ===
            Blocking {taskCount} threads for 5ms...

            """);

        var stopWatch = Stopwatch.StartNew();
        var countDown = new CountdownEvent(taskCount);
        Console.WriteLine("Start QueueUserWorkItem");
        for (var id = 0; id < taskCount; id++)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                Thread.Sleep(5);
                countDown.Signal();
            });
        }
        countDown.Wait();
        countDown.Dispose();
        var queueUserWorkItemMilliseconds = 0 + stopWatch.ElapsedMilliseconds;
        Console.WriteLine($"QueueUserWorkItem test ended with {queueUserWorkItemMilliseconds}ms\n");

        stopWatch = Stopwatch.StartNew();
        countDown = new CountdownEvent(taskCount);
        Console.WriteLine("Start UnsafeQueueUserWorkItem");
        for (var id = 0; id < taskCount; id++)
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                Thread.Sleep(5);
                countDown.Signal();
            }, new object());
        }
        countDown.Wait();
        var unsafeQueueUserWorkItemMilliseconds = 0 + stopWatch.ElapsedMilliseconds;
        Console.WriteLine($"UnsafeQueueUserWorkItem test ended with {unsafeQueueUserWorkItemMilliseconds}ms\n");


        Console.WriteLine($"UnsafeQueueUserWorkItem vs QueueUserWorkItem: faster {(double)queueUserWorkItemMilliseconds / (double)unsafeQueueUserWorkItemMilliseconds}x");
    }
}

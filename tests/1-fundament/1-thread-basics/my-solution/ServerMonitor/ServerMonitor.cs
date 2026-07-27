using System.Diagnostics;

class ServerMonitor
{
    public Random Random { get; } = new Random();
    public ICollection<ServerDescription> Servers { get; set; } = default!;
    public int BackgroundTaskLimit { get; set; }

    public void RunAllChecksAsync()
    {
        var background = Servers.Take(BackgroundTaskLimit).ToHashSet();

        foreach (var server in Servers)
        {
            if (background.Contains(server))
                RunBackgroundCheck(server);

            RunForegroundCheck(server);
        }
    }

    public void RunCriticalChecks()
    {
        var watch = Stopwatch.StartNew();
        var threads = new List<Thread>();

        foreach (var server in Servers)
            threads.Add(RunForegroundCheck(server));

        foreach (var thread in threads)
            thread.Join();

        Console.WriteLine($"\nAll critical checks passed. Total wait time: {watch.ElapsedMilliseconds}ms. Uptime: 100%");
    }

    Thread RunBackgroundCheck(ServerDescription server)
    {
        var background = new Thread(() =>
        {
            var watch = Stopwatch.StartNew();
            Console.WriteLine($"[BACKGROUND] Sending metrics for {server.Name} ({server.Ip}) on thread {Environment.CurrentManagedThreadId}...");
            DoWork();
            Console.WriteLine($"[BACKGROUND] Sending metrics for {server.Name} ({server.Ip}) on thread {Environment.CurrentManagedThreadId} Done — {watch.ElapsedMilliseconds}ms");
        })
        {
            IsBackground = true,
        };
        background.Start();
        return background;
    }

    Thread RunForegroundCheck(ServerDescription server)
    {
        var foreground = new Thread(() =>
        {
            var watch = Stopwatch.StartNew();
            Console.WriteLine($"[CRITICAL] Checking {server.Name} ({server.Ip}) on thread {Environment.CurrentManagedThreadId}...");
            DoWork(true);
            Console.WriteLine($"[CRITICAL] Checking {server.Name} ({server.Ip}) on thread {Environment.CurrentManagedThreadId} Done — {watch.ElapsedMilliseconds}ms");
        })
        {
            IsBackground = false,
        };
        foreground.Start();
        return foreground;
    }

    void DoWork(bool isCritical = false)
    {
        var min = isCritical ? 2 : 5;
        var max = isCritical ? 4 : 7;

        var delay = 0;

        lock (Random)
        {
            delay = Random.Next(min * 1000, (max * 1000) + 1);
        }

        Thread.Sleep(delay);
    }
}

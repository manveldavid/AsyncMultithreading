var backgroundCount = 3;

var servers = new List<(string Name, string Ip)>
{
    ("Auth-DB",     "10.0.1.1"),
    ("Cache-Redis", "10.0.1.2"),
    ("API-Gateway", "10.0.1.3"),
    ("Worker-1",    "10.0.1.4"),
    ("Worker-2",    "10.0.1.5"),
};

var monitor = new ServerMonitor()
{
    Servers = servers.Select(s => new ServerDescription() { Name = s.Name, Ip = s.Ip }).ToArray(),
    BackgroundTaskLimit = backgroundCount
};


Console.WriteLine("Scenario A\n");
monitor.RunAllChecksAsync();
monitor.RunCriticalChecks();

Console.WriteLine("\nScenario B\n");
monitor.RunAllChecksAsync();
Thread.Sleep(100);
Console.WriteLine("\nExiting process now. Background threads will be killed.\n");
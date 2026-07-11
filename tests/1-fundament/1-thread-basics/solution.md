# Решение: Система мониторинга серверов

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

List<(string Name, string Ip)> servers = new()
{
    ("Auth-DB",     "10.0.1.1"),
    ("Cache-Redis", "10.0.1.2"),
    ("API-Gateway", "10.0.1.3"),
    ("Worker-1",    "10.0.1.4"),
    ("Worker-2",    "10.0.1.5"),
};

var monitor = new ServerMonitor(servers);

Console.WriteLine("=== SCENARIO A: RunAllChecksAsync then RunCriticalChecks ===\n");
monitor.RunAllChecksAsync();
monitor.RunCriticalChecks();

Console.WriteLine("\n=== SCENARIO B: RunAllChecksAsync, process exits (background lost) ===\n");
monitor.RunAllChecksAsync();
Thread.Sleep(100);
Console.WriteLine("Main: exiting process now. Background threads will be killed.\n");

public class ServerMonitor
{
    private readonly List<(string Name, string Ip)> _servers;
    private readonly Random _rng = new();

    public ServerMonitor(List<(string Name, string Ip)> servers)
    {
        _servers = servers;
    }

    public void RunAllChecksAsync()
    {
        foreach (var server in _servers)
        {
            var critical = new Thread(() => CriticalCheck(server))
            {
                IsBackground = false,
                Name = $"Critical-{server.Name}"
            };
            critical.Start();
        }

        for (int i = 0; i < Math.Min(3, _servers.Count); i++)
        {
            var server = _servers[i];
            var background = new Thread(() => BackgroundCheck(server))
            {
                IsBackground = true,
                Name = $"Background-{server.Name}"
            };
            background.Start();
        }
    }

    public void RunCriticalChecks()
    {
        var threads = new List<Thread>();
        var sw = Stopwatch.StartNew();

        foreach (var server in _servers)
        {
            var thread = new Thread(() => CriticalCheck(server))
            {
                IsBackground = false,
                Name = $"Critical-{server.Name}"
            };
            thread.Start();
            threads.Add(thread);
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        sw.Stop();
        Console.WriteLine($"\nAll critical checks passed. Total wait time: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine("Uptime: 100%");
    }

    private void CriticalCheck((string Name, string Ip) server)
    {
        int delay = _rng.Next(2000, 4001);
        Console.WriteLine(
            $"[CRITICAL] Checking {server.Name} ({server.Ip}) on thread {Environment.CurrentManagedThreadId}...");
        Thread.Sleep(delay);
        Console.WriteLine(
            $"[CRITICAL] Checking {server.Name} ({server.Ip}) on thread {Environment.CurrentManagedThreadId}... Done — {delay}ms");
    }

    private void BackgroundCheck((string Name, string Ip) server)
    {
        int delay = _rng.Next(5000, 7001);
        Console.WriteLine(
            $"[BACKGROUND] Sending metrics for {server.Name} ({server.Ip}) on thread {Environment.CurrentManagedThreadId}...");
        Thread.Sleep(delay);
        Console.WriteLine(
            $"[BACKGROUND] Sending metrics for {server.Name} ({server.Ip}) on thread {Environment.CurrentManagedThreadId}... Done");
    }
}
```

## Ключевые моменты

1. **Foreground vs Background**: foreground-потоки (`IsBackground = false`) не дают процессу завершиться. Background-потоки (`IsBackground = true`) будут убиты при выходе из `Main`, даже если не завершили работу.

2. **Join()**: блокирует вызывающий поток до завершения целевого потока. Используется в `RunCriticalChecks()` для гарантии, что все критические проверки завершены перед выводом результата.

3. **ManagedThreadId**: каждый поток получает уникальный ID от CLR. Видно, что каждый сервер проверяется на своём потоке ОС.

4. **Сценарий A**: `RunAllChecksAsync()` запускает foreground-потоки, затем `RunCriticalChecks()` через `Join()` ждёт их завершения. Потоки, запущенные в первом вызове, могут совпадать с ожидаемыми во втором — `Join()` дождётся и тех, и других.

5. **Сценарий B**: `RunAllChecksAsync()` запускает потоки и возвращает управление. `Main` завершается через 100ms. Все foreground-потоки держат процесс, пока не завершатся. Background-потоки убиваются сразу при выходе из процесса — вывод `Done` от них отсутствует.

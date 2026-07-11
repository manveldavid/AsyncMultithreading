# Решение: Single-instance приложение и межпроцессная синхронизация через Mutex

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

Console.WriteLine("=== Single-instance check ===\n");

var manager = new LicenseManager();

if (!manager.TryStartApplication())
{
    Console.WriteLine("Exiting second instance.");
    return;
}

Console.WriteLine("\n=== Inter-process file protection ===\n");
manager.SimulateMultiProcessEdit();

Console.WriteLine("\n=== Abandoned Mutex ===\n");
manager.DemonstrateAbandonedMutex();

public class LicenseManager
{
    private Mutex? _appMutex;
    private readonly string _appMutexName = @"Global\LicenseManagerApp";
    private readonly string _fileMutexName = @"Global\LicenseFileMutex";

    public bool TryStartApplication()
    {
        _appMutex = new Mutex(true, _appMutexName, out bool createdNew);

        if (createdNew)
        {
            Console.WriteLine($"License Manager started. PID: {Environment.ProcessId}");
            Console.WriteLine($"Mutex created: {_appMutexName}");
            return true;
        }
        else
        {
            Console.WriteLine("Another instance is already running! Exiting.");
            _appMutex.Dispose();
            _appMutex = null;
            return false;
        }
    }

    public void SimulateMultiProcessEdit()
    {
        var tasks = new Task[3];
        for (int i = 0; i < 3; i++)
        {
            int processId = i + 1;
            tasks[i] = Task.Run(() => EditLicenseFile(processId));
        }
        Task.WhenAll(tasks).Wait();
    }

    private void EditLicenseFile(int processId)
    {
        using var fileMutex = new Mutex(false, _fileMutexName);

        Console.WriteLine($"[Process {processId}] Waiting for LicenseFileMutex...");
        fileMutex.WaitOne();

        try
        {
            Console.WriteLine($"[Process {processId}] Acquired! Editing license file...");
            Thread.Sleep(2000);
            Console.WriteLine($"[Process {processId}] Editing done. Releasing mutex.");
        }
        finally
        {
            fileMutex.ReleaseMutex();
        }
    }

    public void DemonstrateAbandonedMutex()
    {
        const string abandonedName = @"Global\AbandonedDemoMutex";

        var crashTask = Task.Run(() =>
        {
            using var mutex = new Mutex(false, abandonedName);
            Console.WriteLine("[Process C] Acquired mutex — simulating crash...");
            mutex.WaitOne();
            Thread.Sleep(500);
            Console.WriteLine("[Process C] CRASHED! (mutex abandoned)");
            // DOES NOT call ReleaseMutex() — simulates crash
        });

        crashTask.Wait();

        Thread.Sleep(200);

        var recoveryTask = Task.Run(() =>
        {
            using var mutex = new Mutex(false, abandonedName);
            try
            {
                Console.WriteLine("[Process D] Calling WaitOne()...");
                mutex.WaitOne();
                Console.WriteLine("[Process D] WaitOne() succeeded, but... unexpected?");
            }
            catch (AbandonedMutexException ex)
            {
                Console.WriteLine("[Process D] WaitOne() caught AbandonedMutexException!");
                Console.WriteLine($"  Message: {ex.Message}");
                Console.WriteLine("  Mutex abandoned by previous process. Proceeding with caution.");
                Console.WriteLine("  We now own the mutex. State may be corrupt.");
            }
        });

        recoveryTask.Wait();
    }
}
```

## Ключевые моменты

1. **Named Mutex**: `new Mutex(true, @"Global\MyMutex", out createdNew)` — создаёт или подключается к именованному mutex. Префикс `Global\` делает его видимым для всех сессий (Terminal Services).

2. **Single-instance app**: если `createdNew == false` — значит mutex уже существует (приложение уже запущено). Приложение завершается.

3. **WaitOne / ReleaseMutex**: `WaitOne()` блокирует поток, пока mutex не будет захвачен. `ReleaseMutex()` освобождает. Всегда вызывать в `try/finally`.

4. **AbandonedMutexException**: если процесс завершился, не вызвав `ReleaseMutex()`, mutex становится abandoned. Следующий `WaitOne()` бросит `AbandonedMutexException`, но mutex ВСЁ РАВНО будет захвачен.

5. **Mutex vs lock**: Mutex — межпроцессный, lock — только внутри одного процесса. Mutex медленнее (kernel object).

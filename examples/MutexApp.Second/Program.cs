Console.WriteLine("=== MutexApp.Second ===\n");
Console.WriteLine("Attempting to acquire named mutex 'Global\\AsyncMultithreadDemoMutex'...\n");

using var mutex = new Mutex(false, "Global\\AsyncMultithreadDemoMutex");

try
{
    bool acquired = mutex.WaitOne(TimeSpan.FromSeconds(3));

    if (acquired)
    {
        Console.WriteLine("Mutex acquired! No other instance is running.");
        Console.WriteLine("Holding mutex for 10 seconds...");
        Thread.Sleep(10_000);
        Console.WriteLine("Releasing mutex.");
        mutex.ReleaseMutex();
    }
    else
    {
        Console.WriteLine("Could not acquire mutex within 3 seconds.");
        Console.WriteLine("Another instance of the application is already running.");
        Console.WriteLine("\nWaiting for the other instance to release...");

        mutex.WaitOne();
        Console.WriteLine("Mutex acquired after wait! Other instance has released it.");
        Thread.Sleep(2_000);
        mutex.ReleaseMutex();
    }
}
catch (AbandonedMutexException ex)
{
    Console.WriteLine($"Abandoned mutex detected: {ex.Message}");
    Console.WriteLine("The other instance terminated without releasing the mutex.");
}

Console.WriteLine("\nDone. Press any key to exit.");
Console.ReadKey();

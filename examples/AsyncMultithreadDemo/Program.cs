using AsyncMultithreadDemo.Demos;

while (true)
{
    Console.Clear();
    Console.WriteLine("=== Async & Multithreading in .NET ===\n");
    Console.WriteLine(" 1. Thread Basics, ThreadPool, Parallel");
    Console.WriteLine(" 2. async/await, SynchronizationContext");
    Console.WriteLine(" 3. Task Deep Dive (CancellationToken, WhenAny, errors)");
    Console.WriteLine(" 4. Deadlock — how to get and how to fix");
    Console.WriteLine(" 5. async void — the dark side");
    Console.WriteLine(" 6. Task vs ValueTask — benchmark");
    Console.WriteLine(" 7. lock, Monitor, Interlocked");
    Console.WriteLine(" 8. Semaphore — HTTP throttling");
    Console.WriteLine(" 9. volatile, memory ordering");
    Console.WriteLine("10. ThreadLocal vs AsyncLocal");
    Console.WriteLine("11. Dictionary vs ConcurrentDictionary");
    Console.WriteLine("12. Blazor async simulation (InvokeAsync pattern)");
    Console.WriteLine(" 0. Exit\n");
    Console.Write("Select demo: ");

    var key = Console.ReadLine()?.Trim();

    Func<Task>? action = key switch
    {
        "1" => Demo01_ThreadBasics.Run,
        "2" => Demo02_AsyncAwait.Run,
        "3" => Demo03_TaskDeepDive.Run,
        "4" => Demo04_Deadlock.Run,
        "5" => Demo05_AsyncVoid.Run,
        "6" => Demo06_TaskVsValueTask.Run,
        "7" => Demo07_LockDemo.Run,
        "8" => Demo08_SemaphoreDemo.Run,
        "9" => Demo09_VolatileDemo.Run,
        "10" => Demo10_ThreadLocalAsyncLocal.Run,
        "11" => Demo11_DictionaryDemo.Run,
        "12" => Demo12_BlazorAsyncSimulation.Run,
        "0" => null,
        _ => null
    };

    if (key == "0") break;
    if (action is null)
    {
        Console.WriteLine("Unknown option. Press any key...");
        Console.ReadKey(true);
        continue;
    }

    Console.WriteLine($"\n--- Demo {key} ---\n");
    await action();
    Console.WriteLine("\nPress any key to continue...");
    Console.ReadKey(true);
}

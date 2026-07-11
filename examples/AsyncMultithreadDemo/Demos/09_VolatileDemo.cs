namespace AsyncMultithreadDemo.Demos;

public static class Demo09_VolatileDemo
{
    private static volatile bool _volatileFlag;

    public static async Task Run()
    {
        Console.WriteLine("=== volatile, memory ordering, Interlocked ===\n");

        Console.WriteLine("--- 1. volatile: visibility across threads ---\n");
        Console.WriteLine("  volatile prevents compiler/CPU reordering.");
        Console.WriteLine("  Without volatile, a read might be cached in CPU register.");
        Console.WriteLine("  x86 has strong memory model — reordering is rare.");
        Console.WriteLine("  ARM/ARM64 has weaker model — reordering is real.\n");

        Console.WriteLine("  Example: volatile flag for cancellation\n");

        _volatileFlag = false;

        var readerTask = Task.Run(() =>
        {
            int spins = 0;
            while (!_volatileFlag)
            {
                spins++;
                Thread.SpinWait(100);
            }
            Console.WriteLine($"  Reader: flag seen after {spins} spins");
        });

        await Task.Delay(200);
        _volatileFlag = true;
        await readerTask;

        Console.WriteLine("\n--- 2. Interlocked: atomic operations ---\n");

        int value = 0;

        Console.WriteLine("  Interlocked.Increment:");
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 10_000; i++)
                Interlocked.Increment(ref value);
        })).ToArray();

        await Task.WhenAll(tasks);
        Console.WriteLine($"    Result: {value} (expected 100,000)");

        Console.WriteLine("\n  Interlocked.CompareExchange (CAS — compare-and-swap):");
        int shared = 100;

        var casTasks = Enumerable.Range(0, 5).Select(id => Task.Run(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                int original;
                int result;
                do
                {
                    original = shared;
                    result = original + 1;
                }
                while (Interlocked.CompareExchange(ref shared, result, original) != original);
            }
            Console.WriteLine($"    Thread {id} done CAS loop");
        })).ToArray();

        await Task.WhenAll(casTasks);
        Console.WriteLine($"    Result: {shared} (expected 100 + 5*1000 = 5100)");

        Console.WriteLine("\n--- 3. Volatile.Read / Volatile.Write ---\n");
        Console.WriteLine("  Explicit memory barriers (same as volatile field, but for any variable):");

        int data = 0;
        bool ready = false;

        var writer = Task.Run(() =>
        {
            data = 42;
            Volatile.Write(ref ready, true);
        });

        var reader = Task.Run(() =>
        {
            while (!Volatile.Read(ref ready))
                Thread.SpinWait(100);
            Console.WriteLine($"  Reader sees data={data} (guaranteed to see 42 due to Volatile.Write/Read)");
        });

        await Task.WhenAll(writer, reader);

        Console.WriteLine("\n--- 4. When to use what ---\n");
        Console.WriteLine("  volatile:");
        Console.WriteLine("    ✓ Simple flags between threads");
        Console.WriteLine("    ✗ NOT for compound operations (x++ is NOT atomic even with volatile)");
        Console.WriteLine();
        Console.WriteLine("  Interlocked:");
        Console.WriteLine("    ✓ Atomic increment, decrement, exchange, compare-exchange");
        Console.WriteLine("    ✓ Lock-free, highest performance");
        Console.WriteLine();
        Console.WriteLine("  lock:");
        Console.WriteLine("    ✓ Complex critical sections");
        Console.WriteLine("    ✓ Multiple operations that must be atomic together");
        Console.WriteLine();
        Console.WriteLine("  MemoryBarrier / Thread.VolatileRead:");
        Console.WriteLine("    ✓ Full fence — rarely needed in practice");
    }
}

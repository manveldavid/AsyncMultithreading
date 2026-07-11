# Решение: Сервис агрегации цен из маркетплейсов

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

var aggregator = new PriceAggregator();
var product = "iPhone 15 Pro";

Console.WriteLine($"=== Fetching all prices for \"{product}\" ===\n");
var allPrices = await aggregator.FetchAllPricesAsync(product);
if (allPrices.Count > 0)
    Console.WriteLine($"\nBest price: {allPrices[0].Marketplace} — {allPrices[0].Price}₽");

Console.WriteLine($"\n=== Fetch first available price (timeout: 300ms) ===\n");
await aggregator.FetchFirstPriceAsync(product, TimeSpan.FromMilliseconds(300));

Console.WriteLine($"\n=== Fetch with fallback ===\n");
await aggregator.FetchWithFallbackAsync(product);

Console.WriteLine($"\n=== Task.Run vs Task.Factory.StartNew unwrapping ===\n");
aggregator.UnwrapDemo();

Console.WriteLine($"\n=== Show all creation methods ===\n");
aggregator.CreateTasks();

public record PriceResult(string Marketplace, decimal Price, long TimeMs);

public class PriceAggregator
{
    public void CreateTasks()
    {
        // Task.Run
        Task<PriceResult> taskRun = Task.Run(() =>
        {
            Thread.Sleep(100);
            return new PriceResult("Task.Run", 1000, 100);
        });
        taskRun.Wait();

        // Task.Factory.StartNew with LongRunning
        Task<PriceResult> taskLongRunning = Task.Factory.StartNew(() =>
        {
            Thread.Sleep(100);
            return new PriceResult("LongRunning", 2000, 100);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        taskLongRunning.Wait();

        // new Task + Start (cold task)
        var coldTask = new Task<PriceResult>(() =>
        {
            Thread.Sleep(100);
            return new PriceResult("ColdTask", 3000, 100);
        });
        Console.WriteLine($"  new Task status: {coldTask.Status} (Created)");
        coldTask.Start();
        Console.WriteLine($"  after Start status: {coldTask.Status} (WaitingToRun)");
        coldTask.Wait();

        // FromResult (synchronous)
        Task<PriceResult> fromResult = Task.FromResult(new PriceResult("FromResult", 4000, 0));
        Console.WriteLine($"  FromResult status: {fromResult.Status} (RanToCompletion)");

        // CompletedTask
        Task completed = Task.CompletedTask;
        Console.WriteLine($"  CompletedTask status: {completed.Status}");

        // FromException
        Task<PriceResult> fromException = Task.FromException<PriceResult>(
            new TimeoutException("Marketplace unavailable"));
        Console.WriteLine($"  FromException status: {fromException.Status} (Faulted)");
    }

    public async Task<List<PriceResult>> FetchAllPricesAsync(string product)
    {
        var sw = Stopwatch.StartNew();

        var tasks = new List<Task<PriceResult>>
        {
            FetchPriceAsync("Ozon", 320, 89990m, fail: false),
            FetchPriceAsync("Wildberries", 510, 92990m, fail: false),
            FetchPriceAsync("Yandex", 280, 87990m, fail: false),
            Task.Run(async () => { await Task.Delay(800); throw new TimeoutException("AliExpress timed out"); })
                .ContinueWith(_ => new PriceResult("AliExpress", 0, 800), TaskContinuationOptions.NotOnFaulted),
            FetchPriceAsync("MegaMarket", 450, 91990m, fail: false),
        };

        var results = await Task.WhenAll(tasks);
        sw.Stop();

        var successful = new List<PriceResult>();
        foreach (var r in results)
        {
            if (r.Price > 0)
            {
                Console.WriteLine($"[{r.Marketplace,-14}] returned {r.Price}₽ in {r.TimeMs}ms");
                successful.Add(r);
            }
            else
            {
                Console.WriteLine($"[{r.Marketplace,-14}] TIMEOUT/ERROR");
            }
        }

        var sorted = successful.OrderBy(r => r.Price).ToList();
        Console.WriteLine($"Total fetch time: {sw.ElapsedMilliseconds}ms (parallel!)");
        return sorted;
    }

    public async Task FetchFirstPriceAsync(string product, TimeSpan timeout)
    {
        var tasks = new List<Task<PriceResult>>
        {
            FetchPriceAsync("Ozon", 450, 89990m, false),
            FetchPriceAsync("Wildberries", 350, 92990m, false),
            FetchPriceAsync("Yandex", 280, 87990m, false),
        };

        var timeoutTask = Task.Delay(timeout);
        var completedTask = await Task.WhenAny(tasks.Append(
            timeoutTask.ContinueWith(_ => new PriceResult("TIMEOUT", 0, (long)timeout.TotalMilliseconds))
        ));

        if (completedTask.Result.Price > 0)
            Console.WriteLine($"Winner: {completedTask.Result.Marketplace} — {completedTask.Result.Price}₽ in {completedTask.Result.TimeMs}ms");
        else
            Console.WriteLine($"No response within {timeout.TotalMilliseconds}ms — all marketplaces timed out");
    }

    public async Task FetchWithFallbackAsync(string product)
    {
        var ozonTask = Task.Run<PriceResult>(async () =>
        {
            await Task.Delay(100);
            throw new HttpRequestException("Ozon API unavailable");
        });

        try
        {
            var result = await ozonTask;
            Console.WriteLine($"Ozon: {result.Price}₽");
        }
        catch
        {
            Console.WriteLine("Ozon failed, trying Wildberries...");
            try
            {
                var wbResult = await FetchPriceAsync("Wildberries", 200, 92990m, false);
                Console.WriteLine($"Wildberries succeeded: {wbResult.Price}₽");
            }
            catch
            {
                Console.WriteLine("Wildberries also failed — using cached price");
                var cached = await Task.FromResult(new PriceResult("CACHE", 99990m, 0));
                Console.WriteLine($"Cache fallback: {cached.Price}₽");
            }
        }
    }

    public void UnwrapDemo()
    {
        // Task.Run auto-unwraps Task<Task<T>> → Task<T>
        Task<int> autoUnwrapped = Task.Run(async () =>
        {
            await Task.Delay(10);
            return 42;
        });
        Console.WriteLine($"  Task.Run auto-unwrapped type: {autoUnwrapped.GetType().Name}");
        Console.WriteLine($"  Result: {autoUnwrapped.Result}");

        // Task.Factory.StartNew does NOT unwrap
        Task<Task<int>> nested = Task.Factory.StartNew(async () =>
        {
            await Task.Delay(10);
            return 42;
        });
        Console.WriteLine($"  Factory.StartNew type: {nested.GetType().Name}");
        Console.WriteLine($"  Nested inner type: {nested.Result.GetType().Name}");

        // Must manually Unwrap()
        Task<int> unwrapped = nested.Unwrap();
        Console.WriteLine($"  After .Unwrap() result: {unwrapped.Result}");
    }

    private async Task<PriceResult> FetchPriceAsync(string marketplace, int delayMs, decimal price, bool fail)
    {
        var sw = Stopwatch.StartNew();
        await Task.Delay(delayMs);
        sw.Stop();

        if (fail)
            throw new TimeoutException($"{marketplace} unavailable");

        return new PriceResult(marketplace, price, sw.ElapsedMilliseconds);
    }
}
```

## Ключевые моменты

1. **Task.Run vs Factory.StartNew vs new Task**:
   - `Task.Run` — запуск в ThreadPool, авто-unwrap вложенных задач. **Рекомендуемый способ.**
   - `Task.Factory.StartNew` — больше опций (LongRunning, конкретный Scheduler), но НЕ разворачивает `Task<Task<T>>`.
   - `new Task()` — холодная задача, нужно явно `Start()`. Редко используется.

2. **Task.WhenAll** — ожидает завершения ВСЕХ задач. Параллельное выполнение. Если одна упала — остальные продолжаются, но `await WhenAll` бросит исключение.

3. **Task.WhenAny** — ожидает ПЕРВУЮ завершившуюся. Идеально для таймаутов и гонок.

4. **Task.FromResult / CompletedTask / FromException** — «готовые» задачи. Не создают потоки. Используются для кэша, fallback-ов и тестов.

5. **ContinueWith** — низкоуровневое API для цепочек задач. В современном коде лучше использовать `await`.

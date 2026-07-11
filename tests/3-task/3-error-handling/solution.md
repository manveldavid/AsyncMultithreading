# Решение: Платёжный шлюз с надёжной обработкой ошибок

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

var gateway = new PaymentGateway();

Console.WriteLine("=== 1. Sync processing (.Result) — AggregateException ===\n");
var payment1 = new Payment(1, "Stripe", 1500m);
gateway.ProcessPaymentSync(payment1);

Console.WriteLine("\n=== 2. Async processing (await) — unwrapped exception ===\n");
var payment2 = new Payment(2, "PayPal", 2500m);
await gateway.ProcessPaymentAsync(payment2);

Console.WriteLine("\n=== 3. Retry logic ===\n");
var payment3 = new Payment(3, "Stripe", 3500m);
await gateway.ProcessWithRetryAsync(payment3, 3);

Console.WriteLine("\n=== 4. Batch with partial failure ===\n");
var payments = new List<Payment>
{
    new(10, "PayPal", 100m),
    new(11, "Stripe", 200m),
    new(12, "Yandex", 300m),
    new(13, "Stripe", 400m),
    new(14, "PayPal", 500m),
};
await gateway.ProcessBatchWithPartialFailure(payments);

public record Payment(int Id, string Provider, decimal Amount);

public class PaymentException : Exception
{
    public string Provider { get; }
    public PaymentException(string provider, string message, Exception? inner = null)
        : base(message, inner) { Provider = provider; }
}

public class PaymentGateway
{
    public void ProcessPaymentSync(Payment payment)
    {
        Task task = SimulatePaymentAsync(payment);

        try
        {
            task.Wait();
        }
        catch (AggregateException ex)
        {
            Console.WriteLine($"Caught AggregateException with {ex.InnerExceptions.Count} inner exceptions:");
            foreach (var inner in ex.InnerExceptions)
            {
                Console.WriteLine($"  - {inner.GetType().Name}: {inner.Message}");
            }
        }
    }

    public async Task ProcessPaymentAsync(Payment payment)
    {
        try
        {
            await SimulatePaymentAsync(payment);
        }
        catch (PaymentException ex)
        {
            Console.WriteLine($"Caught {ex.GetType().Name}: {ex.Message}");
        }
    }

    public async Task ProcessWithRetryAsync(Payment payment, int maxRetries)
    {
        ExceptionDispatchInfo? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await SimulatePaymentAsync(payment, attempt, maxRetries);
                Console.WriteLine($"[Attempt {attempt}/{maxRetries}] Success!");
                return;
            }
            catch (Exception ex)
            {
                lastException = ExceptionDispatchInfo.Capture(ex);

                if (attempt < maxRetries)
                {
                    int delay = 500 * (int)Math.Pow(2, attempt - 1);
                    Console.WriteLine($"[Attempt {attempt}/{maxRetries}] Failed: {ex.Message}. Retrying in {delay}ms...");
                    await Task.Delay(delay);
                }
                else
                {
                    Console.WriteLine($"[Attempt {attempt}/{maxRetries}] Failed: {ex.Message}. No more retries.");
                }
            }
        }

        lastException?.Throw();
    }

    public async Task ProcessBatchWithPartialFailure(List<Payment> payments)
    {
        Console.WriteLine($"[Batch] Processing {payments.Count} payments...\n");

        var tasks = payments.Select(p => SimulatePaymentAsync(p)).ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
            // WhenAll throws when any task faults — we catch it and inspect each task
        }

        int succeeded = 0;
        int failed = 0;

        for (int i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            var payment = payments[i];

            if (task.IsFaulted)
            {
                failed++;
                Console.WriteLine($"  [Payment #{payment.Id}] {payment.Provider} — FAILED: {task.Exception?.InnerException?.Message}");
            }
            else
            {
                succeeded++;
                Console.WriteLine($"  [Payment #{payment.Id}] {payment.Provider} — OK");
            }
        }

        Console.WriteLine($"\nBatch results: {succeeded} succeeded, {failed} failed.");
    }

    private async Task SimulatePaymentAsync(Payment payment, int attempt = 1, int maxRetries = 1)
    {
        Console.WriteLine($"[Payment #{payment.Id}] Processing via {payment.Provider}...");
        await Task.Delay(200);

        bool shouldFail = payment.Provider switch
        {
            "Stripe" when payment.Id == 3 => attempt < maxRetries, // fails first N-1 times
            "Stripe" => true,  // always fails
            "PayPal" when payment.Id == 2 => true,
            _ => false
        };

        if (shouldFail)
        {
            string reason = payment.Id switch
            {
                1 => "Stripe API timeout",
                11 => "connection timeout",
                13 => "insufficient funds",
                2 => "PayPal balance insufficient",
                3 => "Stripe network error",
                _ => "unknown error"
            };
            throw new PaymentException(payment.Provider, reason);
        }
    }
}
```

## Ключевые моменты

1. **AggregateException при .Wait()/.Result**: когда Task faulted, блокирующие методы оборачивают все исключения в `AggregateException`. Нужно обрабатывать `InnerExceptions`.

2. **await разворачивает исключения**: `await` автоматически извлекает ПЕРВОЕ исключение из AggregateException. Поэтому можно ловить конкретный тип (`catch (PaymentException ex)`) вместо `AggregateException`.

3. **ExceptionDispatchInfo.Capture(ex).Throw()**: сохраняет оригинальный stack trace исключения и перебрасывает его. Важно для ретраев — чтобы не потерять исходное место ошибки.

4. **Partial failure в WhenAll**: если одна из задач падает, `await WhenAll` бросает исключение, но ОСТАЛЬНЫЕ задачи продолжают выполняться. Нужно проверять `task.IsFaulted` для каждой задачи отдельно.

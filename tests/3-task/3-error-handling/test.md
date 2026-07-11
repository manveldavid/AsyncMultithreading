# Задача: Платёжный шлюз с надёжной обработкой ошибок

## Условие

Вы пишете платёжный шлюз, который обрабатывает платежи через несколько провайдеров. Каждый провайдер может упасть. Нужно корректно обрабатывать ошибки, логировать их и ретраить.

### Требования

1. **Класс `PaymentGateway`** с методами обработки платежей.

2. **Метод `ProcessPaymentSync(Payment payment)`** — синхронная обработка через `.Result`:
   - Запускает `Task.Run` для каждого провайдера
   - Вызывает `.Result` для получения результата (НЕ await)
   - Показывает: при `.Result` исключение оборачивается в `AggregateException`
   - Ловит `AggregateException` и выводит ВСЕ внутренние исключения через `InnerExceptions`

3. **Метод `ProcessPaymentAsync(Payment payment)`** — асинхронная обработка через `await`:
   - То же самое, но через `await`
   - Показывает: `await` разворачивает `AggregateException` и бросает ПЕРВОЕ исключение
   - Разница очевидна: `catch (PaymentException ex)` ловит конкретный тип (а не AggregateException)

4. **Метод `ProcessWithRetryAsync(Payment payment, int maxRetries)`** — ретрай-логика:
   - Пытается обработать платёж через основного провайдера
   - При неудаче — ретрай до maxRetries раз с экспоненциальной задержкой
   - Если все ретраи исчерпаны — бросает `ExceptionDispatchInfo.Capture(ex).Throw()` для сохранения stack trace
   - Каждый ретрай логирует: `[Attempt {n}/{max}] Failed: {error}. Retrying in {delay}ms...`

5. **Метод `ProcessBatchWithPartialFailure(List<Payment> payments)`** — батчевая обработка:
   - Запускает все платежи параллельно через `Task.WhenAll`
   - Некоторые платежи могут упасть, другие — успешно
   - Даже если `WhenAll` бросит исключение — собирает результаты успешных платежей
   - Выводит: сколько успешно, сколько упало, и для каждого упавшего — причину
   - Использует `Task.WhenAll` с try/catch и проверкой `task.IsFaulted` для каждого task

6. **Демонстрация трёх сценариев:**
   - Платёж, который всегда падает → показать AggregateException (через .Result) и обычное исключение (через await)
   - Платёж, который падает на первых двух попытках, но проходит на третьей → показать ретрай
   - Батч из 5 платежей, где 2 падают → показать partial failure

### Модели

```csharp
public record Payment(int Id, string Provider, decimal Amount);

public class PaymentException : Exception
{
    public string Provider { get; }
    public PaymentException(string provider, string message, Exception? inner = null)
        : base(message, inner) { Provider = provider; }
}
```

### Ожидаемый вывод

```
=== 1. Sync processing (.Result) — AggregateException ===
[Payment #1] Processing via Stripe...
Caught AggregateException with 1 inner exceptions:
  - PaymentException: Stripe API timeout

=== 2. Async processing (await) — unwrapped exception ===
[Payment #2] Processing via PayPal...
Caught PaymentException: PayPal balance insufficient

=== 3. Retry logic ===
[Payment #3] Processing via Stripe...
[Attempt 1/3] Failed: Stripe network error. Retrying in 500ms...
[Attempt 2/3] Failed: Stripe network error. Retrying in 1000ms...
[Attempt 3/3] Success!

=== 4. Batch with partial failure ===
[Batch] Processing 5 payments...
  [Payment #10] PayPal — OK
  [Payment #11] Stripe — FAILED: timeout
  [Payment #12] Yandex — OK
  [Payment #13] Stripe — FAILED: insufficient funds
  [Payment #14] PayPal — OK

Batch results: 3 succeeded, 2 failed.
```

### Ограничения
- `.Result` / `.Wait()` — только в синхронном методе
- `await` — только в async-методах
- Для ретраев — `ExceptionDispatchInfo.Capture(ex).Throw()`
- Для батча — проверять `task.IsFaulted` и `task.Exception`

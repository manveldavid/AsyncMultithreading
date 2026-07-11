# Задача: Система логирования с Correlation ID — ThreadLocal vs AsyncLocal

## Условие

Вы пишете систему распределённого логирования для микросервисов. Каждый входящий HTTP-запрос получает `CorrelationId`, и все логи этого запроса должны содержать этот ID. Проблема в том, что async-методы могут переключаться между потоками, и `ThreadLocal` «теряет» данные. Нужно использовать `AsyncLocal`.

### Требования

1. **Класс `LoggingContext`** — статический контекст для хранения CorrelationId.

2. **Реализовать ДВА варианта хранения:**
   - `ThreadLocal<string>` — для демонстрации утечки/потери в async-коде
   - `AsyncLocal<string>` — правильный для async-кода

3. **Метод `DemonstrateThreadLocalLeakInThreadPool()`** — утечка в ThreadPool:
   - Создать `ThreadLocal<string>` со значением по умолчанию `"unset"`
   - Задача 1 устанавливает значение `"request-111"`, выводит его
   - Задача 2 (после завершения 1) НЕ устанавливает значение, а просто читает
   - Если Task 2 попадёт на тот же поток ThreadPool — она увидит `"request-111"` (УТЕЧКА!)
   - Показать, что ThreadPool переиспользует потоки → ThreadLocal «протекает»

4. **Метод `DemonstrateThreadLocalLossAcrossAwait()`** — потеря значения при await:
   - `ThreadLocal<string>` — устанавливается до await
   - После `await Task.Delay(50)` — значение МОЖЕТ быть потеряно (поток сменился)
   - Показать: до await — значение есть, после — может быть `"unset"`

5. **Метод `DemonstrateAsyncLocalFlow()`** — правильное поведение AsyncLocal:
   - `AsyncLocal<string>` — устанавливается до await
   - После серии await-ов значение сохраняется на ВСЁМ пути
   - Показать: значение «течёт» через async-цепочку, даже если потоки меняются

6. **Метод `DemonstrateCopyOnWrite()`** — copy-on-write семантика:
   - Родитель устанавливает `AsyncLocal` = `"parent"`
   - Вызывает дочерний метод, который меняет на `"child"`
   - После возврата из дочернего — родитель видит СВОЁ значение `"parent"` (не изменилось!)
   - Показать: изменения в дочернем методе НЕ влияют на родителя

7. **Метод `SimulateRequestPipeline()`** — полный пайплайн запроса:
   - Установить `AsyncLocal<string> CorrelationId` = `Guid`
   - Пройти через: `AuthMiddleware` → `RateLimiterMiddleware` → `Controller` → `Repository` → `Database`
   - Каждый middleware — отдельный async-метод с await
   - Каждый выводит лог с CorrelationId: `[{CorrelationId}] [MiddlewareName] Processing...`
   - Показать, что CorrelationId сохраняется на всём пути

### Ожидаемый вывод

```
=== ThreadLocal Leak in ThreadPool ===
[Task 1, Thread 5] Set ThreadLocal to "request-111"
[Task 2, Thread 5] ThreadLocal = "request-111" ← LEAK! Same thread reused.
[Task 2, Thread 7] ThreadLocal = "unset" ← OK (different thread)
WARNING: ThreadLocal values leak when ThreadPool reuses threads!

=== ThreadLocal Loss Across Await ===
[Before await, Thread 4] ThreadLocal = "before-await"
[After await, Thread 6]  ThreadLocal = "unset" ← LOST! Thread changed.

=== AsyncLocal Flow ===
[Before await, Thread 3] AsyncLocal = "request-999"
[Level 1, Thread 6]      AsyncLocal = "request-999" ← FLOWED correctly!
[Level 2, Thread 8]      AsyncLocal = "request-999" ← Still there!
[After all awaits]       AsyncLocal = "request-999" ← Persisted!

=== Copy-on-Write Semantics ===
[Parent] Set AsyncLocal = "parent"
  [Child] Read AsyncLocal = "parent" (flowed from parent)
  [Child] Set AsyncLocal = "child"
  [Child] Read AsyncLocal = "child"
[Parent] Read AsyncLocal = "parent" ← NOT changed by child!

=== Full Request Pipeline ===
[550e8400-e29b-41d4-a716-446655440000] [AuthMiddleware]     Validating token...
[550e8400-e29b-41d4-a716-446655440000] [RateLimiter]        Checking limits...
[550e8400-e29b-41d4-a716-446655440000] [OrderController]    Processing order #12345
[550e8400-e29b-41d4-a716-446655440000] [OrderRepository]    Querying database...
[550e8400-e29b-41d4-a716-446655440000] [Database]           SELECT * FROM orders...
Request pipeline complete — CorrelationId preserved through all awaits!
```

### Ограничения
- `ThreadLocal<T>` — для демонстрации проблем
- `AsyncLocal<T>` — для правильного решения
- Минимум 5 middleware в пайплайне
- Каждый middleware должен делать `await Task.Delay`
- Показать ManagedThreadId до и после await

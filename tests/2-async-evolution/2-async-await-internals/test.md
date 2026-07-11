# Задача: Симулятор банковских транзакций с детектором подозрительных операций

## Условие

Вы разрабатываете модуль для банковской системы, который обрабатывает транзакции в реальном времени. Ваша задача — понять, как работает `async/await` под капотом (state machine), как `SynchronizationContext` влияет на выполнение, и когда нужно использовать `ConfigureAwait(false)`.

### Требования

1. **Класс `TransactionProcessor`** — обрабатывает банковские транзакции:
   - Метод `AuthorizeTransactionAsync(Transaction tx)` — проверяет баланс, антифрод-проверку, фиксирует транзакцию
   - На каждом шаге есть `await Task.Delay()` для эмуляции IO (проверка по БД, запрос к внешнему API)
   - Выводит `ManagedThreadId` ДО и ПОСЛЕ каждого `await`

2. **Метод `ShowContextFlow()`** — демонстрирует SynchronizationContext:
   - Выводит `SynchronizationContext.Current` до и после `await`
   - Объясняет: в Console — `null`, в WPF/WinForms — Dispatcher, в ASP.NET (legacy) — AspNetSynchronizationContext
   - Показывает: до `await` — поток A, после `await` — поток B (или тот же, зависит от контекста)

3. **Метод `DemonstrateConfigureAwait()`** — показывает разницу `ConfigureAwait(false)`:
   - **Версия без ConfigureAwait(false)**: запустить 10 транзакций, каждая с `await Task.Delay(100)`. После await продолжение захватывает контекст (если он есть). В консоли разницы не будет, но показать `ManagedThreadId` всё равно.
   - **Версия с ConfigureAwait(false)**: то же самое, но после await продолжение выполняется на ThreadPool (другой поток)
   - Вывести таблицу: сколько раз поток сменился после await в каждой версии

4. **Метод `EmulateSynchronizationContext()`** — эмулирует UI-контекст в консоли:
   - Создать кастомный `SingleThreadSynchronizationContext` — очередь задач + один выделенный поток, который их выполняет
   - Установить его как `SynchronizationContext.SetSynchronizationContext()`
   - Запустить транзакцию с `await` внутри этого контекста
   - Показать: ДО await — worker-поток, ПОСЛЕ await — ТОТ ЖЕ worker-поток (continuation вернулся в контекст)
   - Показать: с `ConfigureAwait(false)` продолжение НЕ возвращается в worker-поток

5. **Вывести «state machine trace»** — показать, как выглядит flow:
   ```
   [Main:7] Starting AuthorizeTransactionAsync...
   [Main:7]   Step 1: CheckBalanceAsync — BEFORE await (thread 7)
   [TP:4]    Step 1: CheckBalanceAsync — AFTER await (thread 4) / context changed!
   [TP:4]    Step 2: AntiFraudCheckAsync — BEFORE await (thread 4)
   [TP:6]    Step 2: AntiFraudCheckAsync — AFTER await (thread 6) / context changed!
   ...
   ```
   С ConfigureAwait(false) потоки меняются чаще (ThreadPool распределяет).

### Модель транзакции

```csharp
public record Transaction(int Id, string AccountFrom, string AccountTo, decimal Amount);
```

Создайте 5 тестовых транзакций.

### Ожидаемый вывод (сокращённо)

```
=== SynchronizationContext Flow ===
Before await: SynchronizationContext.Current = null (Console app)
[Thread 3] Before await
[Thread 5] After await — thread changed (no context to capture)

=== ConfigureAwait(false) Comparison ===
Without ConfigureAwait(false):
  Switches: before=[3] → after=[3] (same thread by coincidence)
  Switches: before=[3] → after=[3]
  ...
  Average thread switches: 0.2 per await

With ConfigureAwait(false):
  Switches: before=[4] → after=[7]
  Switches: before=[5] → after=[3]
  ...
  Average thread switches: 0.9 per await

=== Custom SingleThreadSynchronizationContext ===
[Worker:12] Before await — inside custom context
[Worker:12] After await — SAME thread! Context captured the continuation.
[Worker:12] With ConfigureAwait(false) — continuation on ThreadPool [8], NOT worker!

=== State Machine Trace ===
The compiler generates IAsyncStateMachine with states:
  State -1: initial → State 0: after first await → State 1: after second await
  Each await is a suspension point where the state machine saves state and returns.
```

### Ограничения
- Не использовать `Parallel.For`, только `async/await`
- Для кастомного SynchronizationContext — реализовать `Post` и `Send`
- Для эмуляции UI-контекста — использовать выделенный поток с бесконечным циклом обработки очереди

# Задача: Сервис массовой рассылки email-уведомлений

## Условие

Вы разрабатываете бэкенд сервис рассылки email-уведомлений. Сервис получает пачку писем (200+ штук) и должен отправить их через внешний SMTP-шлюз. Создавать по одному потоку на каждое письмо слишком дорого (1 МБ стека на поток), поэтому вы используете **ThreadPool**. Но есть подводные камни.

### Требования

1. **Класс `EmailDispatcher`** должен принимать список email-адресов и отправлять их через ThreadPool. У каждого метода — своя роль.

2. **Метод `SendAllEmails(int emailCount)`** — отправляет N писем через `ThreadPool.QueueUserWorkItem`:
   - Каждое письмо эмулируется задержкой `Thread.Sleep(50-150ms)` (случайная)
   - Выводить: `[ThreadPool-{threadId}] Sending email to user_{id}@company.com... Done`
   - После запуска всех писем вывести статистику ThreadPool и дождаться завершения ВСЕХ писем

3. **Метод `ShowThreadPoolStats(string label)`** — выводит состояние ThreadPool:
   - Min worker threads, Max worker threads
   - Available worker threads (сколько ещё можно занять)
   - Busy worker threads = Max - Available
   - Количество потоков, реально использованных ThreadPool для писем (через подсчёт уникальных `ManagedThreadId`)

4. **Метод `SimulateStarvation(int blockCount)`** — симулирует **ThreadPool Starvation**:
   - Запускает `blockCount` задач, каждая из которых делает **блокирующий** `Thread.Sleep(3000)`
   - Сразу после этого запускает ещё 20 «срочных» писем через `QueueUserWorkItem`
   - Выводить: сколько срочных писем успело выполниться за первые 2 секунды
   - Показать, что starvation замедляет обработку (injection rate = 1 поток/сек)

5. **Метод `CompareQueueUserWorkItemVsUnsafe()`** — сравнивает два API ThreadPool:
   - `QueueUserWorkItem` (передаёт ExecutionContext)
   - `UnsafeQueueUserWorkItem` (не передаёт ExecutionContext)
   - Замерить время выполнения 10,000 быстрых задач каждым способом
   - Показать разницу в производительности и объяснить, почему `Unsafe` быстрее

6. **Метод `ShowHillClimbingEffect()`** — демонстрирует алгоритм Hill Climbing:
   - Показать `ThreadPool.GetMinThreads()` до нагрузки
   - Запустить 50 задач с `Thread.Sleep(500)` — ThreadPool начнёт добавлять потоки
   - Показать `ThreadPoolStats` после нагрузки
   - Объяснить, почему количество потоков выросло

### Ожидаемый вывод (пример)

```
=== ThreadPool Stats (before) ===
Min worker threads: 8, Max: 32767
Available: 32758 | Busy: 9 | Actual threads used: 0

=== Sending 200 emails via ThreadPool ===
[ThreadPool-4] Sending email to user_1@company.com... Done
[ThreadPool-5] Sending email to user_2@company.com... Done
[ThreadPool-3] Sending email to user_3@company.com... Done
...

=== ThreadPool Stats (after) ===
Min worker threads: 8, Max: 32767
Available: 32750 | Busy: 17 | Unique threads used: 12

=== STARVATION DEMO ===
Blocking 10 threads for 3000ms...
[ThreadPool-4] BLOCKED (starvation contributor)
...
Launching 20 urgent emails...
Urgent emails sent in first 2 seconds: 3 (starvation!)

=== QueueUserWorkItem vs UnsafeQueueUserWorkItem ===
QueueUserWorkItem:       45ms
UnsafeQueueUserWorkItem: 28ms
Speedup: 1.6x
```

### Ограничения
- Не использовать `async/await`, только `ThreadPool`
- Для создания множества писем используйте цикл `for`
- Для подсчёта уникальных потоков используйте `ConcurrentDictionary<int, byte>` или `HashSet` с `lock`
- Для ожидания завершения ВСЕХ задач используйте `CountdownEvent` или `ManualResetEvent`
- Нельзя использовать `Task` — только `ThreadPool.QueueUserWorkItem`

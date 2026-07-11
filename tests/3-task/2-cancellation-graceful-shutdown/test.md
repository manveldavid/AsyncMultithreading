# Задача: Фоновый сервис экспорта отчётов с graceful shutdown

## Условие

Вы пишете Windows-сервис для генерации и экспорта отчётов. Сервис должен обрабатывать очередь задач, поддерживать graceful shutdown (при остановке сервиса — доделать текущие задачи и корректно завершиться), уметь отменять долгие операции по таймауту, и всё это с использованием `CancellationToken`.

### Требования

1. **Класс `ReportExportService`** реализует `IDisposable`:
   - Принимает список отчётов для генерации
   - Использует `CancellationTokenSource` для управления жизненным циклом
   - Поддерживает graceful shutdown

2. **Метод `StartAsync()`** — запускает обработку очереди:
   - В бесконечном цикле берёт следующую задачу из очереди
   - На каждой итерации проверяет `ct.IsCancellationRequested`
   - Если отмена запрошена — завершает текущую задачу и выходит
   - Каждый отчёт генерируется 2-5 секунд (эмуляция через `await Task.Delay`)

3. **Метод `StopAsync()`** — graceful shutdown:
   - Вызывает `cts.Cancel()`
   - Ждёт завершения текущей задачи (но не более 10 секунд)
   - Выводит: сколько отчётов успели сгенерировать, сколько осталось в очереди

4. **Метод `GenerateReportWithTimeout(int reportId, int timeoutSeconds)`** — отчёт с таймаутом:
   - Создаёт **linked token**: основной токен + таймаут
   - Если генерация длится дольше timeoutSeconds — отменяется
   - Использует `CancellationTokenSource.CreateLinkedTokenSource()`
   - Выводит: `[Report #{id}] TIMEOUT after {timeout}s` или `[Report #{id}] Generated successfully in {time}s`

5. **Метод `ProcessReportWithProgress(int reportId)`** — отчёт с промежуточными этапами:
   - Этапы: «Querying DB», «Calculating aggregates», «Rendering PDF», «Uploading to S3»
   - На каждом этапе — `ct.ThrowIfCancellationRequested()`
   - Каждый этап — `await Task.Delay(stageDuration, ct)` — передача токена в delay
   - Если отмена пришла во время задержки — `Task.Delay` сам бросит `OperationCanceledException`

6. **Метод `RegisterCleanupHandlers()`** — демонстрирует `Register`:
   - Регистрирует callback через `ct.Register(() => ...)` для:
     - Закрытия соединения с БД
     - Сохранения состояния очереди на диск
     - Отправки уведомления администратору
   - При отмене все callback-и вызываются

7. **Сценарий демонстрации**:
   - Запустить сервис с 5 отчётами
   - Через 6 секунд вызвать `StopAsync()`
   - Показать, какие отчёты успели, какие нет, какие были прерваны

### Ожидаемый вывод

```
=== Report Export Service Started ===
Queue: 5 reports pending

[Report #1] Started...
[Report #1]   Querying DB...
[Report #1]   Calculating aggregates...
[Report #1]   Rendering PDF...
[Report #1] Generated successfully in 3.2s

[Report #2] Started...
[Report #2]   Querying DB...
[Report #2]   Calculating aggregates...
[SHUTDOWN] Stop requested. Finishing current report...
[Report #2]   Rendering PDF... CANCELLED
[Report #2] Interrupted by shutdown.

[CLEANUP] Closing DB connection...
[CLEANUP] Saving queue state (3 reports remaining)...
[CLEANUP] Admin notified.

=== Shutdown Complete ===
Completed: 1 | Interrupted: 1 | Remaining in queue: 3
```

### Ограничения
- Использовать `CancellationTokenSource` и `CancellationToken`
- `ThrowIfCancellationRequested()` на каждом этапе
- `CreateLinkedTokenSource` для таймаутов
- `Register` для cleanup
- `await Task.Delay(..., ct)` для передачи токена в задержку

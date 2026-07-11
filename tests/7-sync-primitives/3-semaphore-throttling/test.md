# Задача: Многопоточный краулер сайтов с throttling через SemaphoreSlim

## Условие

Вы пишете веб-краулер, который обходит страницы сайта и собирает мета-теги. Чтобы не перегрузить сервер (и не получить бан по IP), нужно ограничить количество одновременных запросов. Асинхронный throttling через `SemaphoreSlim` — идеальный инструмент.

### Требования

1. **Класс `WebCrawler`** — краулер с throttling.

2. **Метод `CrawlWithoutThrottling(List<string> urls)`** — без ограничений:
   - Запускает ВСЕ запросы одновременно (20 URL-ов)
   - Каждый «запрос» — `await Task.Delay(200-500ms)` (эмуляция HTTP)
   - Показывает: все 20 запросов стартуют одновременно → сервер перегружен

3. **Метод `CrawlWithThrottling(List<string> urls, int maxConcurrent)`** — с ограничением:
   - Использует `SemaphoreSlim(maxConcurrent)`
   - `await semaphore.WaitAsync()` перед запросом
   - `semaphore.Release()` в finally
   - Только `maxConcurrent` запросов одновременно
   - Показать: запросы выполняются группами по N штук

4. **Метод `CrawlWithTimeout(List<string> urls, int maxConcurrent, TimeSpan timeout)`** — throttling + таймаут:
   - `await semaphore.WaitAsync(timeout)` — ждать слот не более timeout
   - Если слот не получен за timeout — пропустить URL с сообщением `[SKIPPED] Timeout waiting for slot`
   - Остальные URL обрабатываются нормально

5. **Метод `CrawlWithCancellation(List<string> urls, int maxConcurrent)`** — поддержка отмены:
   - Передаёт `CancellationToken` в `semaphore.WaitAsync(ct)`
   - Если отмена запрошена — оставшиеся URL не обрабатываются
   - Graceful shutdown: уже запущенные запросы завершаются

6. **Метод `RunComparison()`** — сравнивает все четыре подхода:
   - Без throttling: самый быстрый, но перегружает
   - С throttling: дольше, но контролируемая нагрузка
   - С таймаутом: часть URL может быть пропущена
   - С отменой: можно остановить краулер в любой момент

### Ожидаемый вывод

```
=== CRAWLING 20 URLs ===

--- Without Throttling ---
[  0ms] Starting ALL 20 requests simultaneously
[220ms] /page1.html — 200 OK (220ms)
[250ms] /page2.html — 200 OK (250ms)
...
[500ms] All 20 completed in 500ms
Server load: 20 concurrent requests (DDoS-level!)

--- With Throttling (max 3) ---
[  0ms] Starting in groups of 3...
[200ms] /page1.html — 200 OK (200ms)
[220ms] /page2.html — 200 OK (220ms)
[250ms] /page3.html — 200 OK (250ms)
[450ms] /page4.html — 200 OK (started after first slot freed)
...
[3500ms] All 20 completed in 3500ms
Server load: max 3 concurrent requests (safe!)

--- With Throttling + Timeout (max 3, timeout 100ms) ---
[  0ms] Starting with 100ms wait timeout...
[100ms] /page15.html — [SKIPPED] Timeout waiting for slot
[200ms] /page1.html — 200 OK (200ms)
...
Completed: 18 | Skipped: 2 (timeout)

--- With Cancellation ---
[  0ms] Starting crawler...
[500ms] Cancellation requested!
[500ms] Graceful shutdown: 2 requests in flight, 10 not started.
Completed: 8 | Cancelled: 12
```

### Ограничения
- Использовать `SemaphoreSlim` (не `Semaphore`)
- `WaitAsync()` для асинхронного ожидания
- `Release()` в finally
- `CancellationToken` поддержка
- Не использовать `lock` или `Parallel.For`

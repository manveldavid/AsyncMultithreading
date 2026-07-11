# Задача: Анализатор логов веб-сервера

## Условие

Вы работаете в DevOps-команде. Вам нужно написать утилиту для анализа access.log веб-сервера. Файл содержит 5 миллионов строк. Каждая строка — это HTTP-запрос с полями: `IP`, `Timestamp`, `Method`, `URL`, `StatusCode`, `ResponseTimeMs`. Вам нужно: посчитать общее количество запросов, найти топ-10 самых медленных URL, посчитать количество ошибок (5xx), построить распределение по методам (GET/POST/PUT/DELETE).

**Ключевой момент:** эту задачу можно решить как через **Concurrency** (один поток, асинхронная обработка чанками), так и через **Parallelism** (Parallel.For на несколько потоков). Вы должны реализовать ОБА подхода и сравнить их.

### Требования

1. **Класс `LogAnalyzer`** с методами-генераторами данных и методами анализа.

2. **Метод `GenerateFakeLogs(int count)`** — генерирует `List<string>` из `count` строк лога. Каждая строка формата:
   ```
   192.168.{subnet}.{host} [11/Jul/2026:14:{min}:{sec}] "GET /api/{resource}/{id} HTTP/1.1" {status} {time}ms
   ```
   Все поля случайные. `status` — с вероятностью 80% 2xx, 15% 4xx, 5% 5xx. `time` — 1-1000ms.

3. **Метод `AnalyzeSequential(List<string> logs)`** — чисто последовательный анализ:
   - Один цикл `foreach` по всем строкам
   - Подсчитывает статистику в одном проходе
   - Замерить время и вывести: `[SEQUENTIAL] Processed {N} rows in {time}ms`

4. **Метод `AnalyzeParallel(List<string> logs)`** — параллельный анализ через `Parallel.For`:
   - Использует `Parallel.For(0, logs.Count, ...)` для распределения работы по ядрам
   - Использует `Interlocked.Increment` для безопасного подсчёта общих счётчиков
   - Использует `ConcurrentDictionary` для сбора статистики по URL
   - Замерить время и вывести: `[PARALLEL] Processed {N} rows in {time}ms. Speedup: {speedup:F1}x`
   - Показать `Environment.ProcessorCount` (сколько ядер доступно)

5. **Метод `AnalyzeConcurrent(List<string> logs)`** — конкурентный (НЕ параллельный) анализ:
   - Делит логи на 4 «чанка» и запускает `ThreadPool.QueueUserWorkItem` для каждого
   - Каждый чанк обрабатывается **последовательно внутри себя**
   - После завершения всех чанков данные агрегируются в основном потоке
   - Использует `CountdownEvent` для ожидания
   - **Важно:** показать, что это Concurrency (чередование), а не Parallelism. Вывести номера потоков для каждого чанка.
   - Замерить время: `[CONCURRENT] Processed {N} rows in {time}ms. Speedup: {speedup:F1}x`

6. **Метод `ShowResults()`** — выводит итоговую статистику:
   - Total requests, Errors (5xx), Error rate %
   - Distribution by HTTP method
   - Top-10 slowest URLs (сортировка по ResponseTimeMs)

7. **Метод `RunBenchmark(int logCount)`** — запускает все три подхода на ОДНИХ И ТЕХ ЖЕ данных, сравнивает время:
   ```
   === BENCHMARK: {logCount:N0} log lines | CPU cores: {cores} ===
   [SEQUENTIAL]   {time}ms
   [CONCURRENT]   {time}ms (speedup: {speedup:F1}x)
   [PARALLEL]     {time}ms (speedup: {speedup:F1}x)
   ```
   И объясняет: почему concurrent быстрее sequential, но параллельный — ещё быстрее (если CPU-bound задача). Или почему concurrent может быть быстрее для IO-bound.

### Ожидаемый вывод (пример)

```
=== BENCHMARK: 5,000,000 log lines | CPU cores: 16 ===
[SEQUENTIAL]   Processed 5,000,000 rows in 2340ms
[CONCURRENT]   Processed 5,000,000 rows in 1280ms (speedup: 1.8x)
               Threads used: [5, 6, 7, 8] — concurrency, not true parallelism
[PARALLEL]     Processed 5,000,000 rows in 310ms (speedup: 7.5x)
               Parallel.For on 16 cores — true parallelism

=== RESULTS ===
Total requests:   5,000,000
Errors (5xx):     248,312 (4.97%)
GET: 3,200,451 | POST: 1,500,230 | PUT: 200,100 | DELETE: 99,219

TOP-10 SLOWEST URLs:
  1. /api/orders/8841         — 998ms
  2. /api/reports/1293        — 995ms
  ...
```

### Ключевой вопрос для размышления

После реализации ответьте на вопрос (в комментариях к коду):
- Почему **Concurrency** даёт прирост над sequential, но не такой большой как **Parallelism**?
- В каком случае Concurrency была бы ЛУЧШЕ Parallelism? (Подсказка: IO-bound задача)

### Ограничения
- В sequential и concurrent НЕ использовать `Parallel.For` — только `foreach` / `ThreadPool`
- Для thread-safe счётчиков использовать `Interlocked.Increment`
- Для thread-safe коллекций использовать `ConcurrentDictionary`
- `Parallel.For` — только в `AnalyzeParallel`
- Логи генерировать 1 раз и передавать одни и те же данные во все три метода
- Не использовать `async/await`

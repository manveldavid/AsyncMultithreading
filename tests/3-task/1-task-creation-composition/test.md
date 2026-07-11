# Задача: Сервис агрегации цен из маркетплейсов

## Условие

Вы пишете агрегатор цен для интернет-магазина. Сервис должен одновременно запрашивать цены на товар из нескольких маркетплейсов (Ozon, Wildberries, Yandex.Market, AliExpress, СберМегаМаркет). Каждый запрос занимает случайное время (200-800ms). Нужно собрать все ответы, найти лучшую цену и учесть таймауты.

### Требования

1. **Класс `PriceAggregator`** должен уметь работать с Task всеми возможными способами.

2. **Метод `CreateTasks()`** — создаёт задачи ВСЕМИ способами, указанными в докладе:
   - `Task.Run` — для CPU-bound эмуляции запроса
   - `Task.Factory.StartNew` с `TaskCreationOptions.LongRunning` — для «тяжёлого» маркетплейса
   - `new Task(...)` + `.Start()` — холодная задача
   - `Task.FromResult` — для закэшированной цены
   - `Task.CompletedTask` — для маркетплейса, который «уже ответил»
   - `Task.FromException` — для маркетплейса, который вернул ошибку
   - Каждая задача возвращает `(string Marketplace, decimal Price)`

3. **Метод `FetchAllPricesAsync(string product)`** — собирает все цены:
   - Запускает все задачи параллельно
   - Использует `Task.WhenAll` для ожидания ВСЕХ
   - Выводит: `[Marketplace] returned {price} in {time}ms` для каждого
   - Возвращает список всех успешных цен, отсортированный по возрастанию

4. **Метод `FetchFirstPriceAsync(string product, TimeSpan timeout)`** — гонка с таймаутом:
   - Запускает все задачи
   - Использует `Task.WhenAny` + `Task.Delay(timeout)` для таймаута
   - Возвращает первый успешный ответ ИЛИ сообщение о таймауте
   - Выводит, какой маркетплейс «победил»

5. **Метод `FetchWithFallbackAsync(string product)`** — цепочка с fallback:
   - Сначала запрашивает Ozon
   - Если Ozon упал с ошибкой — запрашивает Wildberries
   - Если и Wildberries упал — возвращает цену из кэша (`Task.FromResult`)
   - Демонстрирует: `Task` может быть синхронным (FromResult), асинхронным (Run) и faulted (FromException)

6. **Метод `UnwrapDemo()`** — показывает разницу `Task.Run` vs `Task.Factory.StartNew`:
   - `Task.Run` автоматически разворачивает `Task<Task<T>>` → `Task<T>`
   - `Task.Factory.StartNew` возвращает `Task<Task<T>>`, нужно вызывать `.Unwrap()`
   - Показать оба варианта на примере вложенной задачи

### Ожидаемый вывод

```
=== Fetching all prices for "iPhone 15 Pro" ===
[Ozon]         returned 89990₽ in 320ms
[Wildberries]  returned 92990₽ in 510ms
[Yandex]       returned 87990₽ in 280ms
[AliExpress]   TIMEOUT
[MegaMarket]   returned 91990₽ in 450ms

Best price: Yandex — 87990₽

=== Fetch first available price (timeout: 300ms) ===
Winner: Yandex — 87990₽ in 280ms

=== Fetch with fallback ===
Ozon failed, trying Wildberries...
Wildberries succeeded: 92990₽

=== Task.Run vs Task.Factory.StartNew unwrapping ===
Task.Run:            Task<int> (auto-unwrapped)
Task.Factory.StartNew: Task<Task<int>> → need .Unwrap()
```

### Ограничения
- Использовать ВСЕ способы создания задач (Run, StartNew, new Task, FromResult, CompletedTask, FromException)
- WhenAll и WhenAny — обязательно
- Таймаут через WhenAny + Task.Delay
- Fallback через обработку исключений в Task

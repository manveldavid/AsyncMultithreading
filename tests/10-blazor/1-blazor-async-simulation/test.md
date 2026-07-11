# Задача: Эмуляция Blazor Server Dispatcher — дашборд с real-time обновлениями

## Условие

Вы разрабатываете дашборд для отображения метрик сервера в реальном времени. В Blazor Server данные приходят из background-потоков (Timer, Message Bus), но UI-компоненты можно обновлять только через Dispatcher. Без `InvokeAsync` — исключение или некорректное состояние.

Поскольку у нас консоль, нужно **эмулировать** поведение Blazor Server: создать кастомный `SynchronizationContext` (как Dispatcher) и показать все ключевые паттерны.

### Требования

1. **Класс `BlazorDispatcher`** — эмуляция Blazor Server Dispatcher:
   - Реализует `SynchronizationContext` с очередью задач
   - Один выделенный «UI-поток» обрабатывает очередь (как Blazor Server circuit)
   - Метод `InvokeAsync(Func<Task>)` — маршалит задачу в Dispatcher
   - Если задача уже на Dispatcher-потоке — выполняется синхронно (оптимизация)

2. **Класс `MetricsDashboard`** — компонент дашборда (как Blazor компонент):
   - Имеет состояние: `CPU Usage`, `Memory Usage`, `Requests/sec`
   - Метод `UpdateFromBackground(MetricData data)` — вызывается из ThreadPool (Timer callback)
   - Метод `Render()` — отображает состояние (вызывается только через Dispatcher)

3. **Сценарий 1: Ошибка без InvokeAsync** — метод `UpdateWithoutInvokeAsync()`:
   - Background-поток (Timer) пытается обновить состояние напрямую
   - Проверка: `SynchronizationContext.Current != Dispatcher` → бросаем `InvalidOperationException`
   - Показать: «Cross-thread access detected! Use InvokeAsync.»

4. **Сценарий 2: Правильно через InvokeAsync** — метод `UpdateWithInvokeAsync()`:
   - Background-поток вызывает `await dispatcher.InvokeAsync(() => { dashboard.Cpu = value; dashboard.Render(); })`
   - Обновление происходит на правильном потоке
   - Вывод: `[Dispatcher Thread {id}] Updated CPU: {value}%`

5. **Сценарий 3: Таймер с периодическим обновлением** — метод `StartMonitoring()`:
   - `System.Threading.Timer` с интервалом 1 секунда
   - Timer callback выполняется на ThreadPool
   - Внутри callback: `await dispatcher.InvokeAsync(UpdateDashboard)`
   - Показать: данные обновляются каждую секунду, поток Timer ≠ поток Dispatcher

6. **Сценарий 4: OnInitializedAsync timing** — метод `DemonstrateLifecycle()`:
   - Эмуляция `OnInitializedAsync`: долгая загрузка данных
   - Первый Render происходит ДО завершения загрузки (как в Blazor Server!)
   - После загрузки — повторный Render с данными
   - Показать: `[Render #1] Loading...` → `[Data loaded]` → `[Render #2] CPU: 45%, Memory: 72%`

7. **Сценарий 5: JSInterop эмуляция** — метод `DemonstrateJsInterop()`:
   - Эмуляция `IJSRuntime.InvokeAsync<T>()` — всегда async (crossing JS/.NET boundary)
   - Вызов из Dispatcher-потока: `await JsEmulator.CallAsync("getScreenWidth")` → результат приходит асинхронно
   - Показать: даже из UI-потока JS-вызов всегда async

8. **Сравнение Blazor Server vs WASM** — метод `CompareThreadingModels()`:
   - Таблица с различиями: Dispatcher у Server, single-threaded у WASM, InvokeAsync нужен/не нужен, Thread.Sleep vs Task.Delay
   - Комментарии о том, почему в WASM нельзя использовать многопоточность

### Ожидаемый вывод

```
=== Blazor Server Dispatcher Started ===
Dispatcher running on thread 5.

--- Scenario 1: Cross-thread access (WITHOUT InvokeAsync) ---
[Timer Thread 6] Trying to update dashboard directly...
ERROR: Cross-thread access detected! Current thread (6) != Dispatcher thread (5).
Use InvokeAsync to marshal to the Dispatcher.

--- Scenario 2: Correct update (WITH InvokeAsync) ---
[Timer Thread 7] Received metric data, calling InvokeAsync...
[Dispatcher Thread 5] Updated CPU: 78.3%
[Dispatcher Thread 5] Updated Memory: 64.1%
[Dispatcher Thread 5] === DASHBOARD RENDER ===
[Dispatcher Thread 5]   CPU: 78.3% | Memory: 64.1% | Requests: 1423/s

--- Scenario 3: Timer (periodic updates) ---
[Timer Thread 8] Tick #1 → InvokeAsync
[Dispatcher Thread 5] Tick #1: CPU 76.1%, Mem 63.8%, Req 1401/s
[Timer Thread 9] Tick #2 → InvokeAsync
[Dispatcher Thread 5] Tick #2: CPU 82.4%, Mem 65.2%, Req 1503/s
...

--- Scenario 4: OnInitializedAsync timing ---
[Dispatcher Thread 5] === RENDER #1: Loading... ===
[ThreadPool 10] Loading data from API...
[ThreadPool 10] Data loaded! Components: 15, Status: Healthy
[Dispatcher Thread 5] === RENDER #2 ===
[Dispatcher Thread 5]   CPU: 45% | Memory: 72% | Requests: 1234/s
NOTE: First render happens BEFORE data loads (like Blazor Server).

--- Scenario 5: JSInterop emulation ---
[Dispatcher Thread 5] Calling JS: getScreenWidth...
[JS Emulator]   Executing in JS runtime...
[JS Emulator]   getScreenWidth = 1920px
[Dispatcher Thread 5] JS call returned: 1920px
JSInterop is always async — crossing JS/.NET boundary.

--- Blazor Server vs WASM ---
| Aspect            | Blazor Server              | Blazor WASM              |
|-------------------|----------------------------|--------------------------|
| Threading         | Multi-threaded + Dispatcher| Single-threaded (browser)|
| InvokeAsync       | REQUIRED for background    | Not needed               |
| Thread.Sleep      | Blocks circuit thread      | Blocks ENTIRE UI         |
| Task.Delay        | Non-blocking               | Non-blocking (JS timer)  |
| Parallel.For      | Yes (on ThreadPool)        | Runs sequentially         |
```

### Ограничения
- Кастомный SynchronizationContext — обязательно
- Timer должен реально работать на ThreadPool (показать ManagedThreadId)
- Рендер только через Dispatcher
- Не использовать реальный Blazor — только консольная эмуляция

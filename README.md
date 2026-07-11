# Асинхронное и многопоточное программирование в .NET

## Содержание

1. Фундамент: потоки и пулы (~25 мин)
    1. 1. Что такое поток (Thread)
        - Поток ОС vs managed-поток, размер стека (1 МБ по умолчанию)
        - Thread — создание, Start, Join, IsBackground, приоритеты
        - Foreground vs background: почему foreground не даёт процессу завершиться
        - Демо: 01_ThreadBasics.cs — секция "Thread Creation, Join, IsBackground"
    1. 2. ThreadPool
        - Зачем нужен: стоимость создания потока (~1мс + память)
        - Как работает: min/max threads, hill climbing, injection rate
        - ThreadPool.QueueUserWorkItem, UnsafeQueueUserWorkItem
        - ThreadPool starvation — что это и как диагностировать
        - Демо: 01_ThreadBasics.cs — секции "ThreadPool Stats", "QueueUserWorkItem"
    1. 3. Concurrency vs Parallelism
        - Concurrency — структура программы (много задач в progress)
        - Parallelism — исполнение (много задач одновременно)
        - Parallel.For, Parallel.ForEach, Parallel.Invoke
        - Демо: 01_ThreadBasics.cs — секция "Parallel.For vs Sequential"
2. Эволюция асинхронности (~15 мин)
    2. 1. Три эпохи
        - APM (IAsyncResult) — BeginRead/EndRead, callback hell
        - EAP (event-based) — WebClient, события
        - TAP (Task-based) — async/await, стандарт с C# 5
    2. 2. Coroutine vs async/await
        - Coroutine (Unity) — IEnumerator, yield return, ручной драйвер
        - async/await — state machine генерируется компилятором
        - Ключевое отличие: await возвращает управление caller'у, coroutine — нет (без yield)
    2. 3. async/await под капотом
        - IAsyncStateMachine, AsyncTaskMethodBuilder
        - Как компилятор разворачивает await в state machine
        - SynchronizationContext — UI/ASP.NET vs Console
        - Демо: 02_AsyncAwait.cs — разница контекстов, ConfigureAwait(false)
3. Task — основной инструмент (~20 мин)
    3. 1. Task vs Thread
        - Task — это обещание (promise), не поток
        - Task может выполниться синхронно, в пуле, в IO-потоке
    3. 2. Создание и запуск
        - Task.Run, Task.Factory.StartNew, new Task(...)
        - Task.FromResult, Task.CompletedTask, Task.FromException
        - Task.WhenAll, Task.WhenAny — композиция
    3. 3. Галя! Отмена!!!
        - CancellationToken, CancellationTokenSource
        - ThrowIfCancellationRequested, Register, linked tokens
        - Graceful shutdown
    3. 4. Обработка ошибок
        - AggregateException при .Wait(), await разворачивает первое исключение
        - Демо: 03_TaskDeepDive.cs — все подразделы 3.1-3.4 в одном файле
4. Deadlock — как его получить и разрешить (~10 мин)
    4. 1. Классический deadlock 
        - deadlock в UI/ASP.NET контексте `var task = DoAsync(); task.Wait();`
        - Механизм: UI-поток ждёт Task, Task ждёт UI-поток для continuation
    4. 2. Как разрешить
        - ConfigureAwait(false) везде в библиотеках
        - async all the way — не блокировать
        - Task.Run(() => DoAsync()).GetAwaiter().GetResult() — hack для legacy
    4. 3. Два lock-а — залог deadlock-а (deadlock между потоками)
        - Классический пример: Thread 1 захватывает Lock A, ждёт Lock B; Thread 2 — наоборот
    4. 4. Демо
        - Демо: 04_Deadlock.cs — воспроизведение + 4 способа фикса
5. async void — тёмная сторона (~5 мин)
    - Почему существует: обратная совместимость с event handlers
    - Исключения: не попадают в Task, летят в SynchronizationContext
    - Нельзя await, нельзя обработать снаружи
    - Правило: async void только для event handlers, всё остальное — async Task
    - Демо: 05_AsyncVoid.cs — показать "пропадающее" исключение
6. ValueTask (~10 мин)
    6. 1. Проблема Task
        - Класс, аллокация в heap, GC pressure
        - В hot path (кэш, синхронные завершения) — дорого
    6. 2. ValueTask
        - struct, 8 байт, stack allocation
        - Ограничения: один await, нельзя WhenAll
        - IValueTaskSource — переиспользование (как в Socket, FileStream)
    6. 3. Когда использовать
        - Метод завершается синхронно >90% случаев
        - Hot path, миллионы вызовов
        - Демо: 06_TaskVsValueTask.cs — benchmark через Stopwatch + GC.GetTotalMemory
7. Примитивы синхронизации (~25 мин) — ОСНОВНОЙ БЛОК
    7. 1. lock (Monitor)
        - Как работает: object header, thin lock → fat lock
        - Подводные камни: deadlock между двумя lock, reentrancy
        - Monitor.Enter/TryEnter/Exit — явный контроль
        - Демо: 07_LockDemo.cs — секции 1-4 (race condition → lock → Interlocked)
    7. 2. Mutex
        - Межпроцессный, named mutex
        - WaitOne/ReleaseMutex, abandoned mutex
        - Когда нужен: глобальный ресурс, single-instance app
        - Демо: 07_LockDemo.cs — секция 5 (named mutex, удержание 30 сек)
        - Демо: MutexApp.Second — второе приложение, пытается захватить тот же mutex
    7. 3. Semaphore / SemaphoreSlim
        - Ограничение параллелизма: N потоков одновременно
        - SemaphoreSlim — async-friendly, WaitAsync()
        - Сценарии: throttling, connection pool, rate limiter
        - Демо: 08_SemaphoreDemo.cs — throttling HTTP-запросов к http://89.22.229.58:8080
    7. 4. volatile
        - Memory model: reordering, CPU cache coherence
        - volatile — запрещает reordering, но НЕ атомарность
        - Когда нужен: flag-переменные между потоками
        - Когда НЕ нужен: Interlocked, lock, Volatile.Read/Write
        - Демо: 09_VolatileDemo.cs — секции 1-2 (volatile flag, Interlocked)
    7. 5. Interlocked
        - Increment, CompareExchange, Exchange
        - Lock-free примитивы
        - Volatile.Read/Write — явные барьеры
        - Демо: 09_VolatileDemo.cs — секции 2-4 (Interlocked, Volatile.Read/Write)
8. ThreadLocal vs AsyncLocal (~10 мин)
    8. 1. ThreadLocal
        - Данные привязаны к потоку
        - Caveat: в ThreadPool потоки переиспользуются — ThreadLocal может "протекать"
        - Демо: 10_ThreadLocalAsyncLocal.cs — секции 1-2 (ThreadLocal, leak в ThreadPool)
    8. 2. AsyncLocal
        - Данные текут через ExecutionContext
        - Копирование при await (flowing)
        - Сценарии: correlation ID, tenant ID, transaction context
        - Демо: 10_ThreadLocalAsyncLocal.cs — секции 3-5 (AsyncLocal flow, correlation ID)
9. Dictionary vs ConcurrentDictionary (~10 мин)
    9. 1. Почему Dictionary не thread-safe
        - Race condition при resize: потеря данных, infinite loop (в .NET Framework)
        - Reader/writer race
    9. 2. ConcurrentDictionary
        - Lock striping (segmented locking)
        - TryAdd, TryGetValue, AddOrUpdate, GetOrAdd
        - GetOrAdd — valueFactory может вызваться дважды!
        - Snapshot-операции (ToArray, Values) — моментальный снимок
    9. 3. Когда что использовать
        - Read-heavy: lock + Dictionary или ImmutableDictionary + Interlocked.Swap
        - Write-heavy: ConcurrentDictionary
        - Демо: 11_DictionaryDemo.cs — benchmark, показать race в Dictionary
10. Blazor и асинхронность (~15 мин)
    10. 1. Blazor Server vs WebAssembly — модель потоков
        - Blazor Server: SignalR-цикл, один Dispatcher (аналог SynchronizationContext)
        - Blazor WASM: single-threaded (пока), нет настоящих потоков, всё — task-based
        - Почему InvokeAsync нужен в Blazor Server
    10. 2. InvokeAsync / Dispatcher
        - Проблема: background-поток пытается изменить состояние компонента
        - Решение: await InvokeAsync(() => { ... StateHasChanged(); })
        - Демо (консоль): 12_BlazorAsyncSimulation.cs — эмуляция Dispatcher/InvokeAsync через SynchronizationContext
        - Демо (Blazor Server): BlazorServerDemo/Components/Pages/InvokeAsyncDemo.razor — Timer → ThreadPool → InvokeAsync
    10. 3. JSInterop и асинхронность
        - IJSRuntime.InvokeAsync<T>() — всегда async, потому что crossing JS/.NET boundary
        - IJSObjectReference, await using для disposable references
        - Callback из JS в C#: DotNetObjectReference, [JSInvokable]
        - Демо (Blazor Server): BlazorServerDemo/Components/Pages/JsInteropDemo.razor — IJSRuntime + [JSInvokable]
        - Демо (Blazor WASM): BlazorWasmDemo/Pages/JsInteropDemo.razor — JSInterop в WASM
    10. 4. Lifecycle и async
        - OnInitializedAsync — первый render происходит ДО завершения (Server)
        - OnAfterRenderAsync — после рендера
        - StateHasChanged — когда нужен, когда нет (автоматически после lifecycle-методов)
        - Pitfall: StateHasChanged из background-потока без InvokeAsync
        - Демо (Blazor Server): BlazorServerDemo/Components/Pages/LifecycleDemo.razor — OnInitializedAsync timing

## Структура Demo проектов

```
examples\
│
├── AsyncMultithreadDemo\               (console, net10.0) — 12 демо
│   ├── Program.cs                      — меню выбора
│   └── Demos\
│       ├── 01_ThreadBasics.cs          — Thread, ThreadPool, Parallel.For
│       ├── 02_AsyncAwait.cs            — async/await, SyncContext, ConfigureAwait
│       ├── 03_TaskDeepDive.cs          — CancellationToken, WhenAny-timeout, errors
│       ├── 04_Deadlock.cs              — deadlock + 4 фикса
│       ├── 05_AsyncVoid.cs             — async void vs async Task
│       ├── 06_TaskVsValueTask.cs       — Stopwatch + GC benchmark
│       ├── 07_LockDemo.cs              — race → lock → Interlocked + named mutex
│       ├── 08_SemaphoreDemo.cs         — SemaphoreSlim + HttpClient → 89.22.229.58:8080
│       ├── 09_VolatileDemo.cs          — volatile, Interlocked.CompareExchange
│       ├── 10_ThreadLocalAsyncLocal.cs — ThreadLocal leak, AsyncLocal flow
│       ├── 11_DictionaryDemo.cs        — Dictionary race → ConcurrentDictionary
│       └── 12_BlazorAsyncSimulation.cs — Dispatcher/InvokeAsync эмуляция
│
├── MutexApp.Second\                    (console, net10.0)
│   └── Program.cs                      — захват "Global\AsyncMultithreadDemoMutex"
│
├── BlazorServerDemo\                   (blazor server, net10.0)
│   └── Components\Pages\
│       ├── Home.razor                  — навигация
│       ├── InvokeAsyncDemo.razor       — Timer → ThreadPool → InvokeAsync
│       ├── JsInteropDemo.razor         — IJSRuntime + [JSInvokable]
│       └── LifecycleDemo.razor         — OnInitializedAsync timing
│
└── BlazorWasmDemo\                     (blazor wasm, net10.0)
    └── Pages\
        ├── Home.razor                  — навигация
        ├── JsInteropDemo.razor         — JSInterop в WASM
        └── SingleThreadedDemo.razor    — Task.Delay vs Thread.Sleep
```

## 1. Фундамент: потоки и пулы

### 1.1. Что такое поток (Thread)

**Поток (thread)** — это наименьшая единица исполнения, которую планирует операционная система. Каждый процесс имеет как минимум один поток. Потоки одного процесса разделяют память, но имеют собственный стек и регистры процессора.

#### Поток ОС vs managed-поток

- **Поток ОС** создаётся ядром операционной системы (Windows: `CreateThread`, Linux: `pthread_create`)
- **Managed-поток** (.NET) — это обёртка над потоком ОС, управляемая CLR (Common Language Runtime)
- Размер стека по умолчанию: **1 МБ** для 64-разрядных процессов
- Переключение контекста потока: ~**1-10 мкс** (это дорогая операция)

Каждый поток имеет:
- Собственный стек вызовов (1 МБ по умолчанию)
- Собственный набор регистров процессора
- Собственный указатель инструкции (IP)
- Доступ к общей памяти процесса (heap)

#### Класс Thread — базовый примитив

```csharp
var thread = new Thread(() =>
{
    Console.WriteLine($"Working on thread {Environment.CurrentManagedThreadId}");
    Thread.Sleep(1000); // имитация работы
});

thread.Start();       // запуск потока
thread.Join();        // ожидание завершения (блокирует вызывающий поток)
```

#### Ключевые свойства и методы

| Свойство/метод    | Назначение                                                        |
|-------------------|-------------------------------------------------------------------|
| `IsBackground`    | `false` (foreground) — процесс не завершится, пока поток работает |
| `Priority`        | `Lowest`, `BelowNormal`, `Normal`, `AboveNormal`, `Highest`       |
| `Join()`          | Блокирует вызывающий поток до завершения этого потока             |
| `Sleep(ms)`       | Приостанавливает текущий поток на указанное время                 |
| `ManagedThreadId` | Уникальный ID потока в рамках процесса                            |

#### Foreground vs Background потоки

**Foreground-потоки** удерживают процесс живым. Приложение не завершится, пока все foreground-потоки не остановятся.

**Background-потоки** не препятствуют завершению процесса. Когда все foreground-потоки завершаются, все background-потоки автоматически останавливаются.

```csharp
var foreground = new Thread(() =>
{
    Thread.Sleep(5000);
    Console.WriteLine("Foreground thread completed");
}) 
{ IsBackground = false };

var background = new Thread(() =>
{
    Thread.Sleep(5000);
    Console.WriteLine("Background thread completed"); // никогда не выведется
}) 
{ IsBackground = true };

foreground.Start();
background.Start();

// Процесс завершится через 5 секунд, когда foreground закончится
// Background-поток будет убит при завершении процесса
```

**Почему это важно:**
- Если вы создали поток и забыли сделать его `IsBackground = true`, приложение может "зависнуть" при выходе
- По умолчанию `IsBackground = false` (foreground)
- Для фоновых задач всегда явно устанавливайте `IsBackground = true`

#### Приоритеты потоков

```csharp
var highPriority = new Thread(() => DoWork()) 
{ 
    Priority = ThreadPriority.Highest 
};
```

Приоритеты влияют на порядок планирования, но не гарантируют порядок выполнения. Операционная система может игнорировать приоритеты в зависимости от нагрузки.

#### Демонстрация

**Демо:** `01_ThreadBasics.cs` — секция "Thread Creation, Join, IsBackground"

В этом демо вы увидите:
- Создание foreground и background потоков
- Разницу в поведении при завершении процесса
- Использование `Join()` для ожидания
- Вывод `ManagedThreadId` для каждого потока

---

### 1.2. ThreadPool

**ThreadPool** — это пул рабочих потоков, управляемый CLR. Вместо создания нового потока для каждой задачи, ThreadPool переиспользует существующие потоки.

#### Зачем нужен ThreadPool

Создание потока — дорогая операция:
- **Время создания:** ~1 мс
- **Память:** 1 МБ стека на поток
- **Переключение контекста:** ~1-10 мкс

Если ваше приложение обрабатывает тысячи кратковременных задач (HTTP-запросы, IO-операции), создание отдельного потока для каждой задачи неэффективно.

**ThreadPool решает эту проблему:**
- Потоки создаются заранее и переиспользуются
- CLR автоматически управляет количеством потоков
- Оптимизирован для коротких задач

#### Как работает ThreadPool

**Минимальное и максимальное количество потоков:**

```csharp
ThreadPool.GetMinThreads(out int minWorker, out int minIo);
ThreadPool.GetMaxThreads(out int maxWorker, out int maxIo);
ThreadPool.GetAvailableThreads(out int availWorker, out int availIo);

Console.WriteLine($"Min worker threads: {minWorker}");     // обычно = ProcessorCount - количество потоков процессора
Console.WriteLine($"Max worker threads: {maxWorker}");     // обычно = 32767
Console.WriteLine($"Available: {availWorker}");            // сколько можно создать прямо сейчас
```

**Hill Climbing** — алгоритм динамической подстройки количества потоков:
- ThreadPool начинает с минимального количества потоков
- Если задачи выполняются быстро — добавляет потоки
- Если задачи блокируются (IO, lock) — уменьшает потоки
- Цель: максимизировать пропускную способность

**Injection Rate** — скорость создания новых потоков:
- По умолчанию: 1 поток в секунду
- Если все потоки заняты, новый поток создаётся только через 1 секунду
- Это защищает от резкого роста количества потоков

#### Использование ThreadPool

**QueueUserWorkItem** — базовый способ:

```csharp
ThreadPool.QueueUserWorkItem(state =>
{
    Console.WriteLine($"Working on thread {Environment.CurrentManagedThreadId}");
    // state содержит переданные данные
}, "my data");
```

**UnsafeQueueUserWorkItem** — более производительный, но менее безопасный:
- Не передаёт ExecutionContext (данные о безопасности, культуре)
- Используется в высокопроизводительных сценариях
- Может привести к проблемам с безопасностью, если не используется правильно

```csharp
ThreadPool.UnsafeQueueUserWorkItem(state =>
{
    // ExecutionContext НЕ передаётся
    // быстрее, но требует осторожности
}, null);
```

#### ThreadPool Starvation

**Starvation (истощение)** — ситуация, когда все потоки ThreadPool заняты, а новые создаются медленно (1 в секунду).

**Причины starvation:**
1. **Блокирующие вызовы** в async-коде: `Task.Result`, `Task.Wait()`, `Thread.Sleep()`
2. **Долгие синхронные операции** (HTTP-запросы, IO без async)
3. **Deadlocks** — потоки ждут друг друга

**Симптомы:**
- Резкое падение пропускной способности
- Таймауты запросов
- Рост очереди задач в ThreadPool

**Диагностика:**

```csharp
// Мониторинг ThreadPool
ThreadPool.GetAvailableThreads(out int worker, out int io);
ThreadPool.GetMaxThreads(out int maxWorker, out int maxIo);

int inUse = maxWorker - worker;
Console.WriteLine($"ThreadPool in use: {inUse}");

// EventCounters для мониторинга
// System.Threading.ThreadPool.WorkQueueLength
// System.Threading.ThreadPool.ThreadCount
```

**Решение:**
- Используйте async/await вместо блокирующих вызовов
- Избегайте `Thread.Sleep()` в ThreadPool
- Увеличьте `ThreadPool.SetMinThreads()` (но осторожно)

#### Демонстрация

**Демо:** `01_ThreadBasics.cs` — секции "ThreadPool Stats", "QueueUserWorkItem"

В этом демо вы увидите:
- Текущие настройки ThreadPool (min, max, available)
- Запуск 10 задач через `QueueUserWorkItem`
- Замеры времени выполнения (параллельное выполнение)
- Вывод `ManagedThreadId` для каждого потока (потоки переиспользуются)

---

### 1.3. Concurrency vs Parallelism

**Concurrency (конкурентность)** и **Parallelism (параллелизм)** — это разные концепции, которые часто путают.

#### Concurrency — структура программы

**Concurrency** — это способ организации кода, когда несколько задач выполняются **в перекрывающиеся промежутки времени**, но не обязательно одновременно.

**Примеры:**
- Async/await — одна задача ждёт IO, другая работает
- Переключение между задачами на одном ядре
- Обработка нескольких HTTP-запросов на одном потоке (async)

**Ключевая идея:** задачи **чередуются** во времени, но не выполняются одновременно.

```
Task A: ████████░░░░████████
Task B: ░░░░░░░░████░░░░░░░░
Time:   →→→→→→→→→→→→→→→→→→→→
```

#### Parallelism — исполнение

**Parallelism** — это одновременное выполнение нескольких задач **в один и тот же момент времени**.

**Примеры:**
- `Parallel.For` — цикл выполняется на нескольких ядрах
- `Task.Run` — задача выполняется на отдельном потоке
- Математические вычисления на многоядерном процессоре

**Ключевая идея:** задачи выполняются **одновременно** на разных ядрах.

```
Task A: ████████████████████
Task B: ████████████████████
Time:   →→→→→→→→→→→→→→→→→→→→
```

#### Сравнение

| Аспект                 | Concurrency                        | Parallelism          |
|------------------------|------------------------------------|----------------------|
| Определение            | Структура программы                | Исполнение программы |
| Одновременность        | Нет (чередование)                  | Да (одновременно)    |
| Требует несколько ядер | Нет                                | Да                   |
| Пример                 | async/await                        | Parallel.For         |
| Цель                   | Эффективное использование ресурсов | Ускорение вычислений |
| Подходит для           | IO-bound задачи                    | CPU-bound задачи     |

#### Parallel.For, Parallel.ForEach, Parallel.Invoke

**Parallel.For** — параллельный цикл:

```csharp
Parallel.For(0, 100, i =>
{
    Console.WriteLine($"Iteration {i} on thread {Environment.CurrentManagedThreadId}");
    // Каждая итерация может выполняться на разных потоках
});
```

**Parallel.ForEach** — параллельная обработка коллекции:

```csharp
var items = Enumerable.Range(0, 100);
Parallel.ForEach(items, item =>
{
    ProcessItem(item);
});
```

**Parallel.Invoke** — параллельное выполнение методов:

```csharp
Parallel.Invoke(
    () => DoWork1(),
    () => DoWork2(),
    () => DoWork3()
);
```

**Важно:**
- Parallel классы используют ThreadPool
- Не гарантируют порядок выполнения
- Не подходят для IO-bound задач (используйте async/await)
- Подходят для CPU-bound задач (математика, обработка данных)

#### Демонстрация

**Демо:** `01_ThreadBasics.cs` — секция "Parallel.For vs Sequential"

В этом демо вы увидите:
- Последовательный цикл на 100,000,000 итераций
- Параллельный цикл `Parallel.For` с теми же данными
- Замеры времени выполнения
- Вычисление speedup (ускорения)
- Использование `Interlocked.Add` для безопасного суммирования

---

## 2. Эволюция асинхронности

Асинхронное программирование в .NET прошло через три основные эпохи. Каждая следующая решала проблемы предыдущей.

### 2.1. Три эпохи асинхронности

#### APM (Asynchronous Programming Model) — эра Callback

**APM** — первая модель асинхронности в .NET (с .NET Framework 1.0).

**Паттерн:** `BeginXxx` / `EndXxx`

```csharp
// APM — асинхронное чтение из файла
FileStream fs = File.OpenRead("file.txt");
byte[] buffer = new byte[1024];

fs.BeginRead(buffer, 0, buffer.Length, ar =>
{
    int bytesRead = fs.EndRead(ar);
    Console.WriteLine($"Read {bytesRead} bytes");
    
    // Вложенные callback — "callback hell"
    fs.BeginRead(buffer, 0, buffer.Length, ar2 =>
    {
        int bytesRead2 = fs.EndRead(ar2);
        Console.WriteLine($"Read {bytesRead2} bytes");
    }, null);
}, null);
```

**Проблемы APM:**
- **Callback hell** — вложенные callback делают код нечитаемым
- **Сложная обработка ошибок** — исключения нужно обрабатывать в каждом callback
- **Сложная композиция** — трудно выполнить несколько асинхронных операций параллельно
- **Ручное управление ресурсами** — нужно явно вызывать `EndXxx`

#### EAP (Event-based Asynchronous Pattern) — эра событий

**EAP** — попытка упростить асинхронность через события (появилась в .NET Framework 2.0).

**Паттерн:** `XxxAsync` метод + событие `XxxCompleted`

```csharp
// EAP — асинхронная загрузка файла
WebClient client = new WebClient();
client.DownloadStringCompleted += (sender, e) =>
{
    if (e.Error != null)
        Console.WriteLine($"Error: {e.Error.Message}");
    else if (e.Cancelled)
        Console.WriteLine("Cancelled");
    else
        Console.WriteLine($"Result: {e.Result}");
};

client.DownloadStringAsync(new Uri("http://example.com"));
```

**Проблемы EAP:**
- **Состояние рассеяно** — нужно хранить состояние между вызовом и событием
- **Сложная композиция** — всё ещё трудно комбинировать операции
- **Потокобезопасность** — событие может вызваться на другом потоке
- **Не последовательный код** — логика разбросана между методом и обработчиком

#### TAP (Task-based Asynchronous Pattern) — эра async/await

**TAP** — современная модель асинхронности (с C# 5, .NET Framework 4.5).

**Паттерн:** `async/await`, возврат `Task` или `Task<T>`

```csharp
// TAP — асинхронное чтение из файла
async Task ReadFileAsync()
{
    using FileStream fs = File.OpenRead("file.txt");
    byte[] buffer = new byte[1024];

    int bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length);
    Console.WriteLine($"Read {bytesRead} bytes");

    // Вторая операция — последовательно, без вложенности
    int bytesRead2 = await fs.ReadAsync(buffer, 0, buffer.Length);
    Console.WriteLine($"Read {bytesRead2} bytes");
}
```

**Преимущества TAP:**
- **Читаемый код** — выглядит как синхронный
- **Простая обработка ошибок** — try/catch работает как обычно
- **Лёгкая композиция** — `Task.WhenAll`, `Task.WhenAny`
- **Автоматическое управление контекстом** — SynchronizationContext
- **Типобезопасность** — `Task<T>` возвращает результат

**Сравнение трёх моделей:**

| Модель | Синтаксис            | Обработка ошибок | Композиция | Статус            |
|--------|----------------------|------------------|------------|-------------------|
| APM    | `BeginXxx/EndXxx`    | Сложная          | Сложная    | Устарела          |
| EAP    | `XxxAsync` + событие | Средняя          | Сложная    | Устарела          |
| TAP    | `async/await`        | Простая          | Простая    | **Рекомендуется** |

### 2.2. Coroutine vs async/await

**Coroutine** — это обобщённая концепция приостанавливаемых вычислений. В C# они реализованы через `IEnumerable` и `yield return`.

#### Coroutine в C# (yield return)

```csharp
// Coroutine в C#
IEnumerable<int> GenerateNumbers()
{
    yield return 1;
    yield return 2;
    yield return 3;
}

// Использование
foreach (var num in GenerateNumbers())
{
    Console.WriteLine(num);
}
```

#### Coroutine в Unity

Unity использует coroutine для асинхронных операций:

```csharp
// Unity coroutine
IEnumerator LoadData()
{
    Debug.Log("Starting...");
    yield return new WaitForSeconds(2); // ждём 2 секунды
    Debug.Log("After 2 seconds");
    
    yield return StartCoroutine(LoadSubData()); // ждём другую coroutine
    Debug.Log("Sub-data loaded");
}

// Запуск
StartCoroutine(LoadData());
```

**Как работает coroutine в Unity:**
- Unity имеет **ручной драйвер** (Update loop), который вызывает `MoveNext()`
- Coroutine **не возвращает управление** автоматически (без `yield`)
- Coroutine выполняется на **главном потоке** (не параллельно)
- `yield return` приостанавливает выполнение до следующего кадра или события

#### async/await в C#

```csharp
// async/await в C#
async Task LoadData()
{
    Console.WriteLine("Starting...");
    await Task.Delay(2000); // ждём 2 секунды
    Console.WriteLine("After 2 seconds");
    
    await LoadSubData(); // ждём другую async-операцию
    Console.WriteLine("Sub-data loaded");
}

// Запуск
await LoadData();
```

**Как работает async/await:**
- Компилятор генерирует **state machine** автоматически
- `await` **возвращает управление** вызывающему коду
- Продолжение выполняется на **ThreadPool** (или SynchronizationContext)
- Может выполняться **параллельно** на разных потоках

#### Ключевые различия

| Аспект             | Coroutine (Unity)                      | async/await (C#)                      |
|--------------------|----------------------------------------|---------------------------------------|
| Возврат управления | Только при `yield`                     | При каждом `await`                    |
| Поток выполнения   | Главный поток (обычно)                 | ThreadPool или SynchronizationContext |
| Параллелизм        | Нет (последовательно)                  | Да (может быть параллельно)           |
| Драйвер            | Ручной (Unity Update loop)             | Автоматический (state machine)        |
| Обработка ошибок   | Сложная (try/catch в каждом coroutine) | Простая (try/catch как обычно)        |
| Композиция         | Сложная (`StartCoroutine`)             | Простая (`await`, `WhenAll`)          |

**Ключевое отличие:**
- **Coroutine** — это **генератор**, который возвращает значения по одному
- **async/await** — это **promise**, который возвращает результат в будущем

### 2.3. async/await под капотом

Когда вы пишете `async/await`, компилятор генерирует **state machine** — конечный автомат, который управляет выполнением.

#### IAsyncStateMachine

Компилятор создаёт структуру, реализующую `IAsyncStateMachine`:

```csharp
// Исходный код
async Task<int> GetValueAsync()
{
    Console.WriteLine("Before await");
    await Task.Delay(1000);
    Console.WriteLine("After await");
    return 42;
}

// Что генерирует компилятор (упрощённо)
struct GetValueAsyncStateMachine : IAsyncStateMachine
{
    public int State; // текущее состояние
    public AsyncTaskMethodBuilder<int> Builder;
    private TaskAwaiter _awaiter;
    private int _result;
    
    public void MoveNext()
    {
        switch (State)
        {
            case -1: // начальный вызов
                Console.WriteLine("Before await");
                _awaiter = Task.Delay(1000).GetAwaiter();
                if (!_awaiter.IsCompleted)
                {
                    State = 0; // следующее состояние
                    Builder.AwaitUnsafeOnCompleted(ref _awaiter, ref this);
                    return; // возвращаем управление
                }
                goto case 0;
                
            case 0: // продолжение после await
                _awaiter.GetResult(); // проверяет исключения
                Console.WriteLine("After await");
                _result = 42;
                Builder.SetResult(_result);
                break;
        }
    }
}
```

#### AsyncTaskMethodBuilder

`AsyncTaskMethodBuilder<T>` — это билдер, который:
- Создаёт `Task<T>` для возврата
- Управляет state machine
- Устанавливает результат или исключение
- Передаёт продолжение в SynchronizationContext

#### SynchronizationContext

**SynchronizationContext** — это абстракция для "контекста выполнения", которая определяет, где будет выполнено продолжение после `await`.

**Типы SynchronizationContext:**

| Контекст                              | Где используется      | Поведение                         |
|---------------------------------------|-----------------------|-----------------------------------|
| `null`                                | Console, ASP.NET Core | Продолжение на ThreadPool         |
| `WindowsFormsSynchronizationContext`  | WinForms              | Продолжение в UI-потоке           |
| `DispatcherSynchronizationContext`    | WPF                   | Продолжение в UI-потоке           |
| `AspNetSynchronizationContext`        | ASP.NET (legacy)      | Продолжение в контексте запроса   |

**Как работает:**

```csharp
// Console приложение (SynchronizationContext = null)
async Task ConsoleExample()
{
    Console.WriteLine($"Before: {Environment.CurrentManagedThreadId}");
    await Task.Delay(100);
    // Продолжение на ThreadPool (может быть другой поток)
    Console.WriteLine($"After: {Environment.CurrentManagedThreadId}");
}

// WPF приложение (SynchronizationContext = DispatcherSynchronizationContext)
async Task WpfExample()
{
    Console.WriteLine($"Before: {Environment.CurrentManagedThreadId}"); // UI-поток
    await Task.Delay(100);
    // Продолжение в UI-потоке (тот же поток)
    Console.WriteLine($"After: {Environment.CurrentManagedThreadId}"); // UI-поток
}
```

#### ConfigureAwait(false)

**Проблема:** В UI-приложениях `await` возвращает управление в UI-поток, что может вызвать deadlock.

**Решение:** `ConfigureAwait(false)` — не захватывать SynchronizationContext.

```csharp
// Без ConfigureAwait(false)
async Task BadLibraryMethod()
{
    await Task.Delay(100); // захватывает SynchronizationContext
    // Продолжение в UI-потоке (медленно, может вызвать deadlock)
}

// С ConfigureAwait(false)
async Task GoodLibraryMethod()
{
    await Task.Delay(100).ConfigureAwait(false); // не захватывает контекст
    // Продолжение на ThreadPool (быстро, безопасно)
}
```

**Правило:**
- **В библиотеках** — всегда используйте `ConfigureAwait(false)`
- **В UI-коде** — не используйте (нужен UI-поток для обновления интерфейса)
- **В ASP.NET Core** — не нужно (нет SynchronizationContext)

#### Демонстрация

**Демо:** `02_AsyncAwait.cs`

В этом демо вы увидите:
- Базовый async/await — до и после `await`
- `SynchronizationContext.Current` — вывод контекста (null в Console)
- `ConfigureAwait(false)` — разница в поведении
- `Task.WhenAll` — параллельное выполнение async-операций
- Последовательное выполнение vs параллельное — замеры времени

---

## 3. Task — основной инструмент

`Task` — это центральная абстракция в современном асинхронном программировании .NET. Это **обещание (promise)** — объект, представляющий результат, который будет доступен в будущем.

### 3.1. Task vs Thread

**Task** — это не поток, а **абстракция над асинхронной операцией**.

| Аспект            | Thread                        | Task                                              |
|-------------------|-------------------------------|---------------------------------------------------|
| Что это           | Поток ОС                      | Обещание (promise)                                |
| Выполнение        | Всегда на отдельном потоке    | Может быть синхронным, в ThreadPool, в IO-потоке  |
| Результат         | Нет встроенного механизма     | `Task<T>` возвращает результат                    |
| Отмена            | Сложная (флаги, события)      | `CancellationToken`                               |
| Композиция        | Сложная                       | `WhenAll`, `WhenAny`, `ContinueWith`              |
| Обработка ошибок  | События `UnhandledException`  | `AggregateException`, `await` разворачивает       |

**Task может выполниться:**
- **Синхронно** — `Task.FromResult(42)` (результат уже есть)
- **В ThreadPool** — `Task.Run(() => DoWork())`
- **В IO-потоке** — `HttpClient.GetAsync()` (использует IO completion ports)
- **В другом контексте** — зависит от реализации

```csharp
// Task НЕ всегда создаёт поток
var syncTask = Task.FromResult(42); // синхронно, без потока
Console.WriteLine(syncTask.Status); // RanToCompletion

var poolTask = Task.Run(() => 42); // ThreadPool
Console.WriteLine(poolTask.Status); // WaitingToRun → RanToCompletion

var ioTask = httpClient.GetAsync(url); // IO-поток (completion port)
Console.WriteLine(ioTask.Status); // WaitingForActivation
```

### 3.2. Создание и запуск

#### Task.Run — рекомендуемый способ

`Task.Run` — запускает задачу в ThreadPool. Автоматически разворачивает вложенные `Task<Task<T>>`.

```csharp
// Простая задача
Task task = Task.Run(() =>
{
    Console.WriteLine($"Working on {Environment.CurrentManagedThreadId}");
    Thread.Sleep(1000);
});

// Задача с результатом
Task<int> taskWithResult = Task.Run(() =>
{
    Thread.Sleep(1000);
    return 42;
});

int result = await taskWithResult;
```

#### Task.Factory.StartNew — низкоуровневый способ

`Task.Factory.StartNew` — более гибкий, но **не разворачивает** `Task<Task<T>>`.

```csharp
// StartNew НЕ разворачивает Task<Task<T>>
Task<Task<int>> nested = Task.Factory.StartNew(() =>
{
    return Task.FromResult(42);
});

// Нужно явно вызвать Unwrap()
int result = await nested.Unwrap();

// Или используйте Task.Run (разворачивает автоматически)
Task<int> unwrapped = Task.Run(() => Task.FromResult(42));
```

**Когда использовать `Task.Factory.StartNew`:**
- Нужен `TaskCreationOptions.LongRunning` (создать отдельный поток)
- Нужен конкретный `CancellationToken`
- Нужен конкретный `TaskScheduler`

```csharp
Task longRunning = Task.Factory.StartNew(
    () => LongRunningWork(),
    CancellationToken.None,
    TaskCreationOptions.LongRunning, // выделенный поток, не ThreadPool
    TaskScheduler.Default
);
```

#### new Task() — холодная задача

`new Task()` создаёт **холодную** задачу, которую нужно явно запустить через `Start()`.

```csharp
var coldTask = new Task<int>(() =>
{
    Thread.Sleep(1000);
    return 42;
});

Console.WriteLine(coldTask.Status); // Created (не запущена)

coldTask.Start(); // запуск
Console.WriteLine(coldTask.Status); // WaitingToRun

int result = await coldTask;
```

**Когда использовать:**
- Редко. Обычно лучше `Task.Run`.
- Если нужно отложить запуск задачи.

#### Готовые задачи

```csharp
// Задача с готовым результатом
Task<int> fromResult = Task.FromResult(42);

// Завершённая задача (без результата)
Task completed = Task.CompletedTask;

// Задача с исключением
Task<int> fromException = Task.FromException<int>(
    new InvalidOperationException("Error")
);

// Отменённая задача
Task<int> fromCancelled = Task.FromCanceled<int>(
    new CancellationToken(true)
);
```

#### Композиция задач

**Task.WhenAll** — ожидание всех задач:

```csharp
var task1 = DoWorkAsync("Task1", 300);
var task2 = DoWorkAsync("Task2", 500);
var task3 = DoWorkAsync("Task3", 200);

await Task.WhenAll(task1, task2, task3);
// Все задачи завершены
```

**Task.WhenAny** — ожидание первой завершённой задачи:

```csharp
var task1 = DoWorkAsync("Task1", 300);
var task2 = DoWorkAsync("Task2", 500);

var completedTask = await Task.WhenAny(task1, task2);
// completedTask — это task1 (завершилась первой)
```

**WhenAny для таймаута:**

```csharp
var workTask = SlowOperationAsync();
var timeoutTask = Task.Delay(1000);

var completed = await Task.WhenAny(workTask, timeoutTask);

if (completed == timeoutTask)
    Console.WriteLine("Timeout!");
else
    Console.WriteLine($"Result: {workTask.Result}");
```

### 3.3. Галя! Отмена!!!

**CancellationToken** — стандартный механизм отмены в .NET.

#### CancellationTokenSource

```csharp
using var cts = new CancellationTokenSource();

// Передаём токен в задачу
Task task = LongRunningWorkAsync(cts.Token);

// Отменяем через 3 секунды
await Task.Delay(3000);
cts.Cancel();

async Task LongRunningWorkAsync(CancellationToken ct)
{
    for (int i = 0; i < 10; i++)
    {
        ct.ThrowIfCancellationRequested(); // бросает OperationCanceledException
        Console.WriteLine($"Step {i + 1}/10");
        await Task.Delay(1000, ct); // отмена также работает здесь
    }
}
```

#### Register — регистрация callback

```csharp
using var cts = new CancellationTokenSource();

cts.Token.Register(() =>
{
    Console.WriteLine("Token was cancelled!");
    // Очистка ресурсов, закрытие соединений
});

cts.Cancel();
```

#### Linked tokens — связанные токены

```csharp
using var cts1 = new CancellationTokenSource();
using var cts2 = new CancellationTokenSource();
using var linked = CancellationTokenSource.CreateLinkedTokenSource(
    cts1.Token, 
    cts2.Token
);

// linked.Token будет отменён, если отменён ЛЮБОЙ из cts1 или cts2
Task task = DoWorkAsync(linked.Token);

cts2.Cancel(); // отменяем второй — linked тоже отменяется
```

**Сценарии:**
- Отмена по таймауту + отмена пользователем
- Отмена по нескольким условиям

#### Graceful shutdown

```csharp
class WorkerService
{
    private CancellationTokenSource? _cts;
    
    public async Task StartAsync(CancellationToken externalToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                await ProcessNextItemAsync(_cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Shutdown requested");
        }
        finally
        {
            await CleanupAsync();
        }
    }
    
    public void Stop()
    {
        _cts?.Cancel();
    }
}
```

### 3.4. Обработка ошибок

#### AggregateException при .Result / .Wait()

Когда задача faulted, `.Result` и `.Wait()` бросают `AggregateException`, который оборачивает все исключения.

```csharp
var faultedTask = Task.Run(() => 
    throw new InvalidOperationException("Error"));

try
{
    int result = faultedTask.Result; // бросает AggregateException
}
catch (AggregateException ex)
{
    Console.WriteLine($"AggregateException with {ex.InnerExceptions.Count} inner:");
    foreach (var inner in ex.InnerExceptions)
    {
        Console.WriteLine($"  {inner.GetType().Name}: {inner.Message}");
    }
}
```

#### await разворачивает первое исключение

`await` автоматически разворачивает `AggregateException` и бросает **первое** исключение.

```csharp
var faultedTask = Task.Run(() => 
    throw new InvalidOperationException("Error"));

try
{
    await faultedTask; // бросает InvalidOperationException (не AggregateException)
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Caught: {ex.Message}");
}
```

#### ExceptionDispatchInfo — сохранение stack trace

```csharp
try
{
    await task;
}
catch (Exception ex)
{
    // Сохраняем stack trace для повторного бросания
    ExceptionDispatchInfo.Capture(ex).Throw();
}
```

#### Демонстрация

**Демо:** `03_TaskDeepDive.cs`

В этом демо вы увидите:
- Разные способы создания задач (`Task.Run`, `Task.Factory.StartNew`, `new Task()`)
- `Task.FromResult`, `Task.CompletedTask`, `Task.FromException`
- `CancellationToken` — отмена задачи, `ThrowIfCancellationRequested`
- Linked tokens — связанные токены
- `WhenAny` как таймаут
- Обработка ошибок: `AggregateException` vs `await`

---

## 4. Deadlock — как его получить и разрешить

**Deadlock (взаимная блокировка)** — ситуация, когда два или более потока ждут друг друга бесконечно. В .NET это часто происходит при неправильном использовании async/await.

### 4.1. Классический deadlock с .Result / .Wait()

**Сценарий:** UI-приложение (WPF, WinForms) или legacy ASP.NET вызывает async-метод синхронно.

```csharp
// UI-кнопка (WPF)
private void Button_Click(object sender, RoutedEventArgs e)
{
    var task = DoAsync();
    task.Wait(); // DEADLOCK!
    label.Content = task.Result;
}

async Task<string> DoAsync()
{
    await Task.Delay(1000);
    return "Done";
}
```

**Механизм deadlock:**

1. UI-поток вызывает `task.Wait()` и **блокируется**, ожидая завершения задачи
2. `DoAsync()` выполняет `await Task.Delay(1000)`
3. После `await` продолжение должно выполниться в **UI-потоке** (через SynchronizationContext)
4. Продолжение ждёт, пока UI-поток станет доступен
5. UI-поток занят в `Wait()` и ждёт завершения задачи
6. **Круговая зависимость:** UI-поток ждёт задачу, задача ждёт UI-поток

```
UI-поток:   [Wait()] ← ждёт завершения задачи
                ↑
                |
                ↓
Task:       [await] → продолжение → ждёт UI-поток
```

**Где это происходит:**
- **WPF/WinForms** — `DispatcherSynchronizationContext` (один UI-поток)
- **ASP.NET (legacy)** — `AspNetSynchronizationContext` (один контекст запроса)
- **Console/ASP.NET Core** — **НЕ происходит** (нет SynchronizationContext)

### 4.2. Как разрешить deadlock

#### Способ 1: ConfigureAwait(false)

```csharp
// Библиотечный код
async Task<string> DoAsync()
{
    await Task.Delay(1000).ConfigureAwait(false); // не захватывать контекст
    return "Done";
}

// UI-код
private void Button_Click(object sender, RoutedEventArgs e)
{
    var task = DoAsync();
    task.Wait(); // НЕ deadlock — продолжение на ThreadPool
    label.Content = task.Result;
}
```

**Почему работает:** `ConfigureAwait(false)` указывает, что продолжение не нужно возвращать в UI-поток. Оно выполнится на ThreadPool, и `Wait()` завершится успешно.

**Правило:** В библиотеках всегда используйте `ConfigureAwait(false)`.

#### Способ 2: async all the way

```csharp
// Правильно — не блокировать
private async void Button_Click(object sender, RoutedEventArgs e)
{
    var result = await DoAsync(); // НЕ блокируем UI-поток
    label.Content = result;
}

async Task<string> DoAsync()
{
    await Task.Delay(1000);
    return "Done";
}
```

**Почему работает:** `await` не блокирует UI-поток. Он возвращает управление в message loop, и UI остаётся отзывчивым.

**Правило:** Используйте `async` от начала до конца. Не блокируйте async-код.

#### Способ 3: Task.Run wrapper (hack для legacy)

```csharp
// Legacy-код, который нельзя переделать в async
private void Button_Click(object sender, RoutedEventArgs e)
{
    // Запускаем на ThreadPool, чтобы избежать SynchronizationContext
    var result = Task.Run(() => DoAsync()).GetAwaiter().GetResult();
    label.Content = result;
}

async Task<string> DoAsync()
{
    await Task.Delay(1000);
    return "Done";
}
```

**Почему работает:** `Task.Run` запускает задачу на ThreadPool (без SynchronizationContext). Продолжение также выполнится на ThreadPool, и `GetResult()` завершится успешно.

**Когда использовать:** Только в legacy-коде, который нельзя переделать в async. Это **hack**, а не решение.

#### Способ 4: Два lock-а — залог deadlock-а (deadlock между потоками)

Классический deadlock с двумя lock-ами:

```csharp
var lockA = new object();
var lockB = new object();

// Поток 1
Task.Run(() =>
{
    lock (lockA)
    {
        Thread.Sleep(50);
        lock (lockB) // ждёт, пока Поток 2 освободит lockB
        {
            Console.WriteLine("Thread 1: acquired both");
        }
    }
});

// Поток 2
Task.Run(() =>
{
    lock (lockB)
    {
        Thread.Sleep(50);
        lock (lockA) // ждёт, пока Поток 1 освободит lockA
        {
            Console.WriteLine("Thread 2: acquired both");
        }
    }
});
```

**Механизм:**
- Поток 1 захватывает `lockA`, ждёт `lockB`
- Поток 2 захватывает `lockB`, ждёт `lockA`
- Оба ждут бесконечно

**Как избежать:**
- Всегда захватывать lock-и в **одном порядке**
- Использовать `Monitor.TryEnter` с таймаутом
- Избегать вложенных lock-ов

```csharp
// Правильно — всегда в одном порядке
lock (lockA)
{
    lock (lockB)
    {
        // работа
    }
}

// Или с таймаутом
if (Monitor.TryEnter(lockB, TimeSpan.FromMilliseconds(100)))
{
    try
    {
        // работа
    }
    finally
    {
        Monitor.Exit(lockB);
    }
}
else
{
    // Не удалось захватить — обработать ситуацию
}
```

### 4.3. Демо

**Демо:** `04_Deadlock.cs`

В этом демо вы увидите:
- Симуляцию deadlock с кастомным `SynchronizationContext`
- 4 способа разрешения deadlock:
  1. `ConfigureAwait(false)`
  2. `async all the way`
  3. `Task.Run` wrapper
  4. Два lock-а — залог deadlock-а (deadlock между потоками)
- Замеры времени и выводы

---

## 5. async void — тёмная сторона

`async void` — это специальная сигнатура, которая существует только для обратной совместимости с event handlers. В остальном коде используйте `async Task`.

### Почему async void существует

В .NET event handlers имеют сигнатуру `void`:

```csharp
// Event handler в WinForms
button.Click += (sender, e) =>
{
    // обработка клика
};

// Event handler в WPF
button.Click += async (sender, e) =>
{
    await DoWorkAsync(); // async void — единственная возможность
};
```

Когда появился `async/await`, нужно было сохранить совместимость с существующими event-based API. Поэтому разрешили `async void` для event handlers.

### Исключения в async void

**Проблема:** Исключения в `async void` **не попадают в Task** и **не могут быть обработаны** вызывающим кодом.

```csharp
// async Task — исключение можно поймать
async Task ThrowAsyncTask()
{
    await Task.Delay(50);
    throw new InvalidOperationException("from async Task");
}

try
{
    await ThrowAsyncTask();
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Caught: {ex.Message}"); // работает
}

// async void — исключение "пропадает"
async void ThrowAsyncVoid()
{
    await Task.Delay(100);
    throw new InvalidOperationException("from async void");
}

try
{
    ThrowAsyncVoid();
}
catch (Exception ex)
{
    Console.WriteLine($"Caught: {ex.Message}"); // НЕ работает — исключение уже улетело
}
```

**Куда летят исключения:**
- Исключения из `async void` бросаются в `SynchronizationContext`
- В UI-приложениях — в UI-поток (падение приложения)
- В Console/ASP.NET Core — в `AppDomain.UnhandledException` (падение процесса)

```csharp
// Ловим необработанные исключения
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    var ex = (Exception)e.ExceptionObject;
    Console.WriteLine($"Unhandled: {ex.Message}");
};

ThrowAsyncVoid(); // исключение попадёт в UnhandledException
```

### Нельзя await async void

```csharp
// Нельзя await async void
async void DoWorkVoid()
{
    await Task.Delay(1000);
}

// Ошибка компиляции:
! await DoWorkVoid(); // нельзя await void

// Правильно — async Task
async Task DoWorkTask()
{
    await Task.Delay(1000);
}

await DoWorkTask(); // работает
```

### Правило использования

**Используйте `async void` ТОЛЬКО для:**
- Event handlers (`button.Click += async (s, e) => ...`)
- Переопределения методов с сигнатурой `void` (rare cases)

**Используйте `async Task` для:**
- Всех остальных async-методов
- Методов, которые могут быть вызваны из других async-методов
- Методов, которые должны возвращать результат или исключение

```csharp
// ПЛОХО
async void LoadData()
{
    var data = await GetDataAsync();
    UpdateUI(data);
}

// ХОРОШО
async Task LoadDataAsync()
{
    var data = await GetDataAsync();
    UpdateUI(data);
}

// ИСКЛЮЧЕНИЕ — event handler
button.Click += async (sender, e) =>
{
    await LoadDataAsync();
};
```

### Fire-and-forget паттерн

Если вам нужен fire-and-forget (запустил и забыл), используйте `async Task` с discard:

```csharp
// ПЛОХО — async void
async void FireAndForget()
{
    await DoWorkAsync();
}

FireAndForget();

// ХОРОШО — discard
async Task DoWorkAsync()
{
    await Task.Delay(1000);
}

_ = DoWorkAsync(); // fire-and-forget, но исключение можно обработать

// ИЛИ — Task.Run для background
_ = Task.Run(() => DoWorkAsync());

// ИЛИ — ContinueWith для обработки результата
DoWorkAsync().ContinueWith(t =>
{
    if (t.IsFaulted)
        Console.WriteLine($"Error: {t.Exception?.InnerException?.Message}");
});
```

### Демо

**Демо:** `05_AsyncVoid.cs`

В этом демо вы увидите:
- Сравнение `async Task` и `async void`
- Как исключение "пропадает" в `async void`
- Как поймать необработанное исключение через `AppDomain.UnhandledException`
- Fire-and-forget паттерн

---

## 6. ValueTask

`ValueTask` — это легковесная альтернатива `Task` для сценариев, где метод часто завершается синхронно.

### 6.1. Проблема Task

**Task<T>** — это **класс**, который всегда аллоцируется в heap.

**Проблемы:**
- **Аллокация в heap** — каждый `Task<T>` создаёт объект в памяти
- **GC pressure** — много кратковременных задач нагружают garbage collector
- **Неэффективно для синхронных завершений** — если результат уже есть, мы всё равно создаём объект

```csharp
// Кэш в памяти — результат уже есть
async Task<int> GetFromCacheAsync(int key)
{
    if (_cache.TryGetValue(key, out int value))
        return value; // аллокация Task, хотя результат уже есть!
    
    return await LoadFromDbAsync(key);
}

// Вызываем миллион раз
for (int i = 0; i < 1_000_000; i++)
{
    await GetFromCacheAsync(i); // 1,000,000 аллокаций Task в heap
}
```

**В hot path (кэш, частые вызовы)** это создаёт избыточную нагрузку на GC.

### 6.2. ValueTask

**ValueTask<T>** — это **структура** (struct), которая аллоцируется на стеке.

**Преимущества:**
- **Stack allocation** — не нагружает heap
- **8 байт** — размер структуры (vs ~48 байт для Task)
- **Нет GC pressure** — не создаёт мусор для сборщика

```csharp
// ValueTask — нет аллокаций для синхронных завершений
async ValueTask<int> GetFromCacheAsync(int key)
{
    if (_cache.TryGetValue(key, out int value))
        return value; // stack allocation, нет heap!
    
    return new ValueTask<int>(LoadFromDbAsync(key));
}

// Вызываем миллион раз
for (int i = 0; i < 1_000_000; i++)
{
    await GetFromCacheAsync(i); // 0 аллокаций в heap для кэша
}
```

**Как работает ValueTask:**
- Если результат уже есть — хранит его внутри структуры (stack)
- Если результат в будущем — хранит ссылку на `Task<T>` (heap)
- Автоматически переключается между режимами

#### Ограничения ValueTask

**1. Нельзя await дважды:**

```csharp
ValueTask<int> vt = GetValueAsync();

int result1 = await vt; // OK
int result2 = await vt; // InvalidOperationException!
```

**Почему:** ValueTask может быть потреблён только один раз. После `await` он становится недействительным.

**2. Нельзя использовать с Task.WhenAll:**

```csharp
ValueTask<int> vt1 = GetValueAsync();
ValueTask<int> vt2 = GetValueAsync();

// Ошибка компиляции:
// await Task.WhenAll(vt1, vt2); // Task.WhenAll требует Task

// Решение — конвертировать в Task (аллокация!):
await Task.WhenAll(vt1.AsTask(), vt2.AsTask());
```

**3. Нельзя использовать ContinueWith:**

```csharp
ValueTask<int> vt = GetValueAsync();

// Ошибка компиляции:
// vt.ContinueWith(t => ...); // нет такого метода

// Решение — конвертировать в Task:
vt.AsTask().ContinueWith(t => ...);
```

#### IValueTaskSource — переиспользование

`IValueTaskSource<T>` — интерфейс для пулинга `ValueTask`, чтобы избежать аллокаций даже для асинхронных завершений.

**Используется в:**
- `Socket` — асинхронные сетевые операции
- `FileStream` — асинхронный IO
- `HttpClient` — асинхронные HTTP-запросы

```csharp
// Упрощённый пример пулинга
class PooledValueTaskSource : IValueTaskSource<int>
{
    private int _result;
    private Exception? _error;
    private ManualResetValueTaskSourceCore<int> _core;
    
    public int GetResult(short token) => _core.GetResult(token);
    
    public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);
    
    public void OnCompleted(Action<object?> continuation, object? state, short token, 
        ValueTaskSourceOnCompletedFlags flags)
    {
        _core.OnCompleted(continuation, state, token, flags);
    }
    
    public void SetResult(int result)
    {
        _result = result;
        _core.SetResult(result);
    }
    
    public void SetException(Exception error)
    {
        _error = error;
        _core.SetException(error);
    }
}
```

**Преимущества:**
- Переиспользует один объект для множества операций
- Нет аллокаций даже для асинхронных завершений
- Используется в высокопроизводительных библиотеках

### 6.3. Когда использовать ValueTask

**Используйте ValueTask, когда:**
- Метод завершается **синхронно >90% случаев** (кэш, валидация)
- **Hot path** — миллионы вызовов в секунду
- **GC pressure** критично (high-performance приложения)

**НЕ используйте ValueTask, когда:**
- Метод **всегда асинхронный** (HTTP-запросы, DB calls)
- Нужна **композиция** (`WhenAll`, `WhenAny`, `ContinueWith`)
- Нужно **await дважды**
- Не уверены — используйте `Task` (безопаснее)

**Правило:**
> Если вы не измеряли performance и не видите проблему с GC — используйте `Task`. `ValueTask` — для оптимизации hot path.

### 6.4. Benchmark

**Демо:** `06_TaskVsValueTask.cs`

В этом демо вы увидите:
- Benchmark: 1,000,000 вызовов `Task<int>` vs `ValueTask<int>`
- Замеры времени через `Stopwatch`
- Замеры аллокаций через `GC.GetTotalMemory`
- Сравнение speedup и экономии памяти
- Демонстрация ограничений `ValueTask` (нельзя await дважды)

**Ожидаемые результаты:**
- `Task<int>`: ~100-200ms, ~40 MB аллокаций
- `ValueTask<int>`: ~50-100ms, ~0 MB аллокаций (для синхронных завершений)
- Speedup: 2-4x
- Экономия памяти: 40+ MB

---

## 7. Примитивы синхронизации

Примитивы синхронизации обеспечивают безопасный доступ к общим ресурсам из нескольких потоков.

### 7.1. lock (Monitor)

**lock** — самый распространённый примитив синхронизации. Гарантирует, что только один поток выполняет код в критической секции.

#### Как работает

```csharp
private readonly object _lockObj = new object();
private int _counter = 0;

void Increment()
{
    lock (_lockObj)
    {
        _counter++; // только один поток может выполнять этот код одновременно
    }
}
```

**Под капотом:**
- `lock(obj)` компилируется в `Monitor.Enter(obj)` + `try/finally` + `Monitor.Exit(obj)`
- Использует **object header** (8 байт в начале каждого объекта)
- **Thin lock** — быстрая блокировка (несколько наносекунд)
- **Fat lock** — медленная блокировка (если есть конкуренция)

```csharp
// Что генерирует компилятор
lock (obj)
{
    // критическая секция
}

// Эквивалент
Monitor.Enter(obj);
try
{
    // критическая секция
}
finally
{
    Monitor.Exit(obj);
}
```

#### Подводные камни

**1. Deadlock между двумя lock:**

```csharp
var lockA = new object();
var lockB = new object();

// Поток 1
lock (lockA)
{
    Thread.Sleep(50);
    lock (lockB) // ждёт, пока Поток 2 освободит lockB
    {
        Console.WriteLine("Thread 1");
    }
}

// Поток 2
lock (lockB)
{
    Thread.Sleep(50);
    lock (lockA) // ждёт, пока Поток 1 освободит lockA
    {
        Console.WriteLine("Thread 2");
    }
}
```

**Решение:** Всегда захватывать lock-и в одном порядке.

**2. Reentrancy (повторный захват):**

```csharp
var lockObj = new object();

lock (lockObj)
{
    lock (lockObj) // OK — тот же поток может захватить lock повторно
    {
        // reentrancy разрешён
    }
}
```

**3. Блокировка на value types:**

```csharp
// ПЛОХО — boxing создаёт новый объект каждый раз
int value = 42;
lock (value) // boxing! каждый раз новый объект
{
    // не работает как ожидается
}

// ХОРОШО — только reference types
private readonly object _lock = new object();
lock (_lock)
{
    // работает корректно
}
```

#### Monitor.Enter/TryEnter/Exit — явный контроль

```csharp
var lockObj = new object();

// TryEnter с таймаутом
if (Monitor.TryEnter(lockObj, TimeSpan.FromMilliseconds(100)))
{
    try
    {
        // критическая секция
    }
    finally
    {
        Monitor.Exit(lockObj);
    }
}
else
{
    Console.WriteLine("Не удалось захватить lock за 100ms");
}
```

#### Демо

**Демо:** `07_LockDemo.cs` — секции 1-4

В этом демо вы увидите:
- Race condition: `counter++` из 10 потоков → потеря данных
- `lock` — исправление race condition
- `Interlocked.Increment` — lock-free альтернатива
- Замеры: без lock / lock / Interlocked

### 7.2. Mutex

**Mutex (Mutual Exclusion)** — межпроцессный примитив синхронизации. В отличие от `lock`, может использоваться между разными процессами.

#### Named Mutex — межпроцессная синхронизация

```csharp
// Создаём именованный mutex
using var mutex = new Mutex(false, "Global\\MyAppMutex");

// Захватываем
mutex.WaitOne();
try
{
    // критическая секция (работает между процессами)
}
finally
{
    mutex.ReleaseMutex();
}
```

**Глобальный vs локальный:**
- `"Global\\MyAppMutex"` — виден всем сессиям (Terminal Services)
- `"Local\\MyAppMutex"` — виден только в текущей сессии
- `"MyAppMutex"` (без префикса) — зависит от контекста

#### Abandoned Mutex

Если процесс завершается, не освободив mutex, он становится **abandoned**:

```csharp
try
{
    mutex.WaitOne();
}
catch (AbandonedMutexException ex)
{
    Console.WriteLine($"Mutex abandoned: {ex.Message}");
    // Предыдущий процесс не освободил mutex
    // Мы всё равно владеем mutex сейчас
}
```

#### Single-instance application

Mutex часто используется для предотвращения запуска нескольких экземпляров приложения:

```csharp
static void Main()
{
    using var mutex = new Mutex(true, "Global\\MyAppSingleInstance", out bool createdNew);
    
    if (!createdNew)
    {
        Console.WriteLine("Another instance is already running");
        return;
    }
    
    // Запуск приложения
    Application.Run(new MainForm());
}
```

#### Демо

**Демо:** `07_LockDemo.cs` — секция 5 (named mutex)

В этом демо вы увидите:
- Создание named mutex
- Удержание mutex в течение 30 секунд
- Запуск второго приложения, которое пытается захватить тот же mutex

**Демо:** `MutexApp.Second` — второе приложение

Запустите это приложение, пока первое удерживает mutex:
- Первое приложение: `dotnet run --project AsyncMultithreadDemo` → выберите демо 7
- Второе приложение: `dotnet run --project MutexApp.Second`

Вы увидите, как второе приложение обнаруживает, что mutex уже занят.

### 7.3. Semaphore / SemaphoreSlim

**Semaphore** ограничивает количество потоков, которые могут одновременно acceder к ресурсу.

#### SemaphoreSlim — async-friendly

```csharp
var semaphore = new SemaphoreSlim(3); // максимум 3 потока одновременно

async Task ProcessRequestAsync(int requestId)
{
    await semaphore.WaitAsync(); // async ожидание
    try
    {
        Console.WriteLine($"Request {requestId} started");
        await Task.Delay(1000); // имитация работы
        Console.WriteLine($"Request {requestId} completed");
    }
    finally
    {
        semaphore.Release();
    }
}

// Запускаем 10 запросов, но только 3 выполняются одновременно
var tasks = Enumerable.Range(1, 10).Select(i => ProcessRequestAsync(i));
await Task.WhenAll(tasks);
```

**Преимущества SemaphoreSlim:**
- **Async-friendly** — `WaitAsync()` не блокирует поток
- **CancellationToken** — поддержка отмены
- **Лёгкий** — не использует kernel objects

#### Semaphore (kernel-level)

```csharp
var semaphore = new Semaphore(3, 3); // initial, maximum

semaphore.WaitOne(); // блокирующий вызов
try
{
    // работа
}
finally
{
    semaphore.Release();
}
```

**Когда использовать Semaphore (kernel):**
- Межпроцессная синхронизация (named semaphore)
- Нужна синхронизация с unmanaged-кодом

#### Сценарии использования

**1. Throttling HTTP-запросов:**

```csharp
var semaphore = new SemaphoreSlim(5); // максимум 5 одновременных запросов

async Task<HttpResponseMessage> ThrottledGetAsync(HttpClient client, string url)
{
    await semaphore.WaitAsync();
    try
    {
        return await client.GetAsync(url);
    }
    finally
    {
        semaphore.Release();
    }
}
```

**2. Connection pool:**

```csharp
class ConnectionPool
{
    private readonly SemaphoreSlim _semaphore;
    private readonly Queue<Connection> _connections = new();
    
    public ConnectionPool(int maxConnections)
    {
        _semaphore = new SemaphoreSlim(maxConnections, maxConnections);
    }
    
    public async Task<Connection> AcquireAsync(CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        lock (_connections)
        {
            return _connections.Dequeue();
        }
    }
    
    public void Release(Connection conn)
    {
        lock (_connections)
        {
            _connections.Enqueue(conn);
        }
        _semaphore.Release();
    }
}
```

**3. Rate limiter:**

```csharp
class RateLimiter
{
    private readonly SemaphoreSlim _semaphore;
    private readonly TimeSpan _interval;
    
    public RateLimiter(int maxRequests, TimeSpan interval)
    {
        _semaphore = new SemaphoreSlim(maxRequests, maxRequests);
        _interval = interval;
    }
    
    public async Task<bool> TryAcquireAsync()
    {
        bool acquired = await _semaphore.WaitAsync(TimeSpan.Zero);
        if (acquired)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(_interval);
                _semaphore.Release();
            });
        }
        return acquired;
    }
}
```

#### Демо

**Демо:** `08_SemaphoreDemo.cs`

В этом демо вы увидите:
- `SemaphoreSlim(3)` — throttling HTTP-запросов
- 20 запросов к `http://89.22.229.58:8080`
- Сравнение: без throttling vs с throttling
- Замеры времени и статус-кодов

### 7.4. volatile

**volatile** — модификатор, который запрещает компилятору и CPU переупорядочивать операции с переменной.

#### Memory model и reordering

**Проблема:** Компилятор и CPU могут переупорядочивать операции для оптимизации.

```csharp
int data = 0;
bool ready = false;

// Поток 1
data = 42;
ready = true;

// Поток 2
if (ready)
{
    Console.WriteLine(data); // может вывести 0, если reordering произошёл
}
```

**Reordering:**
- Компилятор может переместить `ready = true` перед `data = 42`
- CPU может выполнить операции в другом порядке
- В результате Поток 2 видит `ready = true`, но `data = 0`

#### volatile запрещает reordering

```csharp
int data = 0;
volatile bool ready = false;

// Поток 1
data = 42;
ready = true; // volatile write — барьер

// Поток 2
if (ready) // volatile read — барьер
{
    Console.WriteLine(data); // всегда выведет 42
}
```

**volatile гарантирует:**
- Запись в volatile переменную выполняется **после** всех предыдущих операций
- Чтение volatile переменной выполняется **до** всех последующих операций
- **Не гарантирует атомарность** — `x++` всё ещё не атомарно даже с volatile

#### Когда использовать volatile

**Используйте volatile для:**
- Флагов между потоками (`_shouldStop`, `_isReady`)
- Простых переменных, которые читаются/пишутся из разных потоков

```csharp
private volatile bool _shouldStop = false;

void Worker()
{
    while (!_shouldStop)
    {
        // работа
    }
}

void Stop()
{
    _shouldStop = true;
}
```

**НЕ используйте volatile для:**
- Составных операций (`x++`, `x += 10`)
- Когда нужна атомарность — используйте `Interlocked`
- Когда нужна сложная синхронизация — используйте `lock`

#### Volatile.Read / Volatile.Write

Явные барьеры памяти (эквивалент volatile, но для любых переменных):

```csharp
int data = 0;
bool ready = false;

// Поток 1
data = 42;
Volatile.Write(ref ready, true); // явный барьер записи

// Поток 2
if (Volatile.Read(ref ready)) // явный барьер чтения
{
    Console.WriteLine(data); // всегда 42
}
```

### 7.5. Interlocked

**Interlocked** — lock-free атомарные операции. Быстрее `lock`, но ограничен набором операций.

#### Increment / Decrement

```csharp
int counter = 0;

// Атомарный инкремент (lock-free)
Interlocked.Increment(ref counter);

// Атомарный декремент
Interlocked.Decrement(ref counter);

// Эквивалентно counter++, но атомарно и быстрее lock
```

#### CompareExchange (CAS — compare-and-swap)

```csharp
int value = 100;

// Атомарная операция: если value == expected, то value = newValue
int original = Interlocked.CompareExchange(ref value, newValue: 200, comparand: 100);

if (original == 100)
{
    Console.WriteLine("Exchange succeeded");
}
else
{
    Console.WriteLine($"Exchange failed, current value: {original}");
}
```

**CAS-цикл (lock-free алгоритм):**

```csharp
int shared = 0;

void LockFreeIncrement()
{
    int original;
    int result;
    do
    {
        original = shared;
        result = original + 1;
    }
    while (Interlocked.CompareExchange(ref shared, result, original) != original);
}
```

#### Exchange

```csharp
int value = 42;

// Атомарно устанавливает новое значение и возвращает старое
int old = Interlocked.Exchange(ref value, 100);

Console.WriteLine($"Old: {old}, New: {value}"); // Old: 42, New: 100
```

#### Add

```csharp
int value = 0;

// Атомарно добавляет значение
Interlocked.Add(ref value, 10);
```

#### Когда что использовать

| Примитив      | Когда использовать                            | Производительность    |
|---------------|-----------------------------------------------|-----------------------|
| `lock`        | Сложные критические секции                    | Медленнее             |
| `Interlocked` | Простые атомарные операции (increment, swap)  | Быстрее               |
| `volatile`    | Флаги между потоками                          | Быстрее, но ограничен |

**Правило:**
- Если нужна атомарность одной операции — `Interlocked`
- Если нужна атомарность нескольких операций — `lock`
- Если нужен простой флаг — `volatile`

#### Демо

**Демо:** `09_VolatileDemo.cs`

В этом демо вы увидите:
- `volatile` flag — видимость между потоками
- `Interlocked.Increment` — атомарный инкремент
- `Interlocked.CompareExchange` — CAS-цикл
- `Volatile.Read/Write` — явные барьеры

---

## 8. ThreadLocal vs AsyncLocal

`ThreadLocal<T>` и `AsyncLocal<T>` — это способы хранения данных, привязанных к контексту выполнения. Но они работают по-разному.

### 8.1. ThreadLocal<T>

**ThreadLocal<T>** — данные привязаны к **потоку**. Каждый поток имеет свою копию значения.

#### Базовое использование

```csharp
var threadLocal = new ThreadLocal<string>(() => $"default-{Environment.CurrentManagedThreadId}");

// Главный поток
threadLocal.Value = "main-thread-value";
Console.WriteLine(threadLocal.Value); // "main-thread-value"

// Другой поток
Task.Run(() =>
{
    Console.WriteLine(threadLocal.Value); // "default-{threadId}" — своё значение
    threadLocal.Value = "pool-thread-value";
    Console.WriteLine(threadLocal.Value); // "pool-thread-value"
}).Wait();

// Главный поток — значение не изменилось
Console.WriteLine(threadLocal.Value); // "main-thread-value"
```

#### Caveat: утечка в ThreadPool

**Проблема:** ThreadPool переиспользует потоки. Значение `ThreadLocal` может "протекать" между задачами.

```csharp
var leakLocal = new ThreadLocal<string>(() => "uninitialized");

// Задача 1 устанавливает значение
var t1 = Task.Run(() =>
{
    leakLocal.Value = "task-1-set-this";
    Console.WriteLine($"Task 1 (thread {Environment.CurrentManagedThreadId}): {leakLocal.Value}");
});
t1.Wait();

// Задача 2 может увидеть значение от Задачи 1, если поток переиспользуется
var t2 = Task.Run(() =>
{
    Console.WriteLine($"Task 2 (thread {Environment.CurrentManagedThreadId}): {leakLocal.Value}");
    // Может вывести "task-1-set-this" — утечка!
});
t2.Wait();
```

**Как избежать:**
- Всегда инициализировать значение в начале задачи
- Использовать `AsyncLocal` для async-кода
- Очищать значение в конце задачи

#### Когда использовать ThreadLocal

**Используйте для:**
- Per-thread кэшей
- Генераторов случайных чисел (`Random`)
- Данных, которые не должны пересекать потоки

```csharp
// Per-thread Random (избегаем конкуренции)
var threadLocalRandom = new ThreadLocal<Random>(() => new Random());

int GetRandom()
{
    return threadLocalRandom.Value!.Next();
}
```

### 8.2. AsyncLocal<T>

**AsyncLocal<T>** — данные текут через **ExecutionContext** (async flow). Значение "течёт" через `await`.

#### Базовое использование

```csharp
var asyncLocal = new AsyncLocal<string>();

async Task Main()
{
    asyncLocal.Value = "root-value";
    Console.WriteLine($"Root: {asyncLocal.Value}"); // "root-value"
    
    await Level1();
    
    Console.WriteLine($"Root (after async): {asyncLocal.Value}"); // "root-value"
}

async Task Level1()
{
    Console.WriteLine($"Level 1: {asyncLocal.Value}"); // "root-value" — значение flowed
    asyncLocal.Value = "level-1-value";
    await Level2();
    Console.WriteLine($"Level 1 (after Level 2): {asyncLocal.Value}"); // "level-1-value"
}

async Task Level2()
{
    Console.WriteLine($"Level 2: {asyncLocal.Value}"); // "level-1-value"
    asyncLocal.Value = "level-2-value";
    await Task.Delay(50);
    Console.WriteLine($"Level 2 (after await): {asyncLocal.Value}"); // "level-2-value"
}
```

**Ключевое поведение:**
- Значение **течёт вниз** (от родителя к ребёнку)
- Изменения в ребёнке **не влияют** на родителя (copy-on-write)
- Работает через `await`

#### ExecutionContext и flowing

**ExecutionContext** — это контекст выполнения, который включает:
- `SecurityContext` (безопасность)
- `Thread.CurrentPrincipal` (пользователь)
- `CultureInfo` (культура)
- `AsyncLocal<T>` значения

При `await` ExecutionContext **копируется** и передаётся в продолжение.

```csharp
var asyncLocal = new AsyncLocal<string>();

async Task Parent()
{
    asyncLocal.Value = "parent";
    await Child();
    Console.WriteLine($"Parent: {asyncLocal.Value}"); // "parent" — не изменилось
}

async Task Child()
{
    Console.WriteLine($"Child: {asyncLocal.Value}"); // "parent" — flowed from parent
    asyncLocal.Value = "child"; // изменяем свою копию
    Console.WriteLine($"Child (after change): {asyncLocal.Value}"); // "child"
}
```

#### Сценарии использования

**1. Correlation ID для логирования:**

```csharp
public static class LoggingContext
{
    public static AsyncLocal<string> CorrelationId { get; } = new();
}

async Task HandleRequestAsync(HttpRequest request)
{
    LoggingContext.CorrelationId.Value = Guid.NewGuid().ToString();
    
    await ProcessRequestAsync(request);
}

async Task ProcessRequestAsync(HttpRequest request)
{
    // Все логи в этой цепочке имеют одинаковый CorrelationId
    Logger.LogInformation($"Processing request {LoggingContext.CorrelationId.Value}");
    await CallServiceAsync();
}

async Task CallServiceAsync()
{
    Logger.LogInformation($"Calling service {LoggingContext.CorrelationId.Value}");
}
```

**2. Tenant ID в multi-tenant приложениях:**

```csharp
public static class TenantContext
{
    public static AsyncLocal<string> TenantId { get; } = new();
}

// Middleware устанавливает TenantId
app.Use(async (context, next) =>
{
    TenantContext.TenantId.Value = context.Request.Headers["X-Tenant-Id"];
    await next();
});

// В любом месте кода можно получить TenantId
async Task GetDataAsync()
{
    string tenantId = TenantContext.TenantId.Value;
    var data = await _repository.GetDataByTenantAsync(tenantId);
}
```

**3. Transaction context:**

```csharp
public static class TransactionContext
{
    public static AsyncLocal<Transaction> Current { get; } = new();
}

async Task WithTransactionAsync(Func<Task> operation)
{
    var transaction = new Transaction();
    TransactionContext.Current.Value = transaction;
    
    try
    {
        await operation();
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}

// Использование
await WithTransactionAsync(async () =>
{
    await UpdateAccountAsync();
    await UpdateBalanceAsync();
    // Все операции в одной транзакции
});
```

#### Сравнение ThreadLocal vs AsyncLocal

| Аспект                | ThreadLocal<T>            | AsyncLocal<T>                         |
|-----------------------|---------------------------|---------------------------------------|
| Привязка              | К потоку                  | К ExecutionContext                    |
| Flow через await      | Нет                       | Да                                    |
| Изменения в ребёнке   | Влияют на поток           | Не влияют на родителя                 |
| ThreadPool утечка     | Да                        | Нет                                   |
| Использование         | Per-thread кэши, Random   | Correlation ID, tenant, transaction   |

**Правило:**
- **ThreadLocal** — для данных, которые не должны пересекать потоки
- **AsyncLocal** — для данных, которые должны течь через async-цепочку

#### Демо

**Демо:** `10_ThreadLocalAsyncLocal.cs`

В этом демо вы увидите:
- `ThreadLocal<T>` — значение на поток
- Утечка `ThreadLocal` в ThreadPool
- `AsyncLocal<T>` — значение течёт через async
- Correlation ID — практический пример

---

## 9. Dictionary vs ConcurrentDictionary

`Dictionary<TKey, TValue>` — не thread-safe. `ConcurrentDictionary<TKey, TValue>` — thread-safe альтернатива.

### 9.1. Почему Dictionary не thread-safe

**Проблема 1: Race condition при resize**

Когда `Dictionary` заполняется, он увеличивает внутренний массив (resize). Если два потока одновременно делают resize, данные могут потеряться.

```csharp
var dict = new Dictionary<int, int>();

// 10 потоков одновременно записывают
var tasks = Enumerable.Range(0, 10).Select(id => Task.Run(() =>
{
    for (int i = 0; i < 10_000; i++)
    {
        dict[i] = i; // race condition
    }
})).ToArray();

await Task.WhenAll(tasks);

Console.WriteLine($"Count: {dict.Count}"); // может быть меньше 100_000
// Возможны исключения: ArgumentException, IndexOutOfRangeException
```

**Что происходит:**
- Два потока одновременно видят, что нужен resize
- Оба начинают resize
- Данные теряются или повреждаются
- В .NET Framework возможен infinite loop (в .NET Core исправлено)

**Проблема 2: Reader/writer race**

```csharp
var dict = new Dictionary<int, int>();

// Поток 1 читает
Task.Run(() =>
{
    while (true)
    {
        if (dict.TryGetValue(42, out int value))
        {
            Console.WriteLine(value);
        }
    }
});

// Поток 2 пишет
Task.Run(() =>
{
    for (int i = 0; i < 1000; i++)
    {
        dict[i] = i;
    }
});

// Reader может видеть частично обновлённые данные
// Возможны исключения
```

### 9.2. ConcurrentDictionary

**ConcurrentDictionary** — thread-safe коллекция, оптимизированная для параллельного доступа.

#### Lock striping (segmented locking)

**Как работает:**
- Внутренний массив разделён на **сегменты** (buckets)
- Каждый сегмент имеет свой lock
- Разные потоки могут работать с разными сегментами одновременно

```
Dictionary:     [  lock  |  lock  |  lock  | ... ]
                    ↑ все операции блокируют весь словарь
ConcurrentDictionary:
                [ lock1 | lock2 | lock3 | lock4 | ... ]
                   ↑       ↑       ↑       ↑
                разные потоки работают с разными сегментами
```

**Преимущества:**
- Меньше конкуренции между потоками
- Выше производительность при параллельной записи

#### API

**TryAdd / TryGetValue:**

```csharp
var cd = new ConcurrentDictionary<string, int>();

// Безопасное добавление
bool added = cd.TryAdd("key", 42);
if (added)
{
    Console.WriteLine("Added successfully");
}

// Безопасное чтение
if (cd.TryGetValue("key", out int value))
{
    Console.WriteLine($"Value: {value}");
}
```

**AddOrUpdate:**

```csharp
// Добавляет или обновляет атомарно
cd.AddOrUpdate(
    "counter",
    addValueFactory: key => 1, // если ключа нет
    updateValueFactory: (key, oldValue) => oldValue + 1 // если ключ есть
);
```

**GetOrAdd:**

```csharp
// Возвращает существующее значение или добавляет новое
int value = cd.GetOrAdd("key", key => ComputeExpensiveValue(key));
```

**⚠️ Caveat: GetOrAdd valueFactory может вызваться дважды!**

```csharp
var cd = new ConcurrentDictionary<string, string>();

int factoryCalls = 0;

var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
{
    cd.GetOrAdd("key", k =>
    {
        Interlocked.Increment(ref factoryCalls);
        Thread.Sleep(10); // имитация дорогой операции
        return "value";
    });
})).ToArray();

await Task.WhenAll(tasks);

Console.WriteLine($"Factory called {factoryCalls} times"); // может быть > 1!
```

**Почему:**
- Два потока одновременно видят, что ключа нет
- Оба начинают вычислять valueFactory
- Один успевает добавить первым
- Второй видит, что значение уже есть, и отбрасывает своё

**Решение:**
- Если valueFactory имеет side effects — используйте `AddOrUpdate` или `Lazy<T>`
- Если valueFactory идемпотентен — `GetOrAdd` подходит

```csharp
// С Lazy<T> — valueFactory вызывается только один раз
var cd = new ConcurrentDictionary<string, Lazy<string>>();

var lazy = cd.GetOrAdd("key", k => new Lazy<string>(() => ComputeExpensiveValue(k)));
string value = lazy.Value; // вычисление происходит только один раз
```

#### Snapshot-операции

**ToArray, Values, Keys** — возвращают **моментальный снимок** (snapshot):

```csharp
var cd = new ConcurrentDictionary<int, int>();

// Поток 1 добавляет
Task.Run(() =>
{
    for (int i = 0; i < 1000; i++)
        cd[i] = i;
});

// Поток 2 читает snapshot
var snapshot = cd.ToArray(); // снимок на момент вызова
Console.WriteLine($"Snapshot count: {snapshot.Length}");
```

**Важно:**
- Snapshot не изменяется после создания
- Операции `ToArray`, `Values`, `Keys` — O(n)
- Для больших словарей могут быть дорогими

### 9.3. Когда что использовать

| Сценарий                          | Рекомендация                                                      |
|-----------------------------------|-------------------------------------------------------------------|
| Read-heavy, редкие writes         | `lock + Dictionary` или `ImmutableDictionary + Interlocked.Swap`  |
| Write-heavy, высокая конкуренция  | `ConcurrentDictionary`                                            |
| Простые атомарные операции        | `ConcurrentDictionary` (TryAdd, GetOrAdd)                         |
| Snapshot-чтение                   | `ConcurrentDictionary` или `ImmutableDictionary`                  |
| Read-dominated, lock-free         | `ImmutableDictionary + Interlocked.Swap`                          |

**Benchmark:**

```csharp
// lock + Dictionary
var dict = new Dictionary<int, int>();
var lockObj = new object();

lock (lockObj)
{
    dict[key] = value;
}

// ConcurrentDictionary
var cd = new ConcurrentDictionary<int, int>();
cd[key] = value;
```

**Производительность:**
- `lock + Dictionary` — быстрее для read-heavy (lock только на write)
- `ConcurrentDictionary` — быстрее для write-heavy (fine-grained locking)

**Правило:**
- Если не уверены — используйте `ConcurrentDictionary` (безопаснее)
- Если нужна максимальная производительность — benchmark оба варианта

#### Демо

**Демо:** `11_DictionaryDemo.cs`

В этом демо вы увидите:
- `Dictionary` race condition — потеря данных, исключения
- `ConcurrentDictionary` — корректная работа
- `GetOrAdd` — valueFactory вызывается дважды
- Benchmark: `lock + Dictionary` vs `ConcurrentDictionary`

---

## 10. Blazor и асинхронность

Blazor — фреймворк для создания веб-UI на C#. Есть две модели хостинга: **Blazor Server** и **Blazor WebAssembly (WASM)**. Они имеют разные модели потоков и асинхронности.

### 10.1. Blazor Server vs WebAssembly — модель потоков

#### Blazor Server

**Архитектура:**
- Компоненты выполняются на **сервере**
- UI обновляется через **SignalR** (WebSocket)
- Каждый пользователь имеет **circuit** (соединение) с сервером

**Модель потоков:**
- Есть **Dispatcher** — аналог `SynchronizationContext`
- Dispatcher гарантирует, что обновления UI выполняются **последовательно**
- Все изменения состояния компонента должны проходить через Dispatcher

```
[Browser] ←→ [SignalR] ←→ [Blazor Server]
                              ↓
                         [Dispatcher]
                              ↓
                      [Component State]
```

**Почему InvokeAsync нужен:**
- Background-потоки (ThreadPool, Timer) не имеют доступа к Dispatcher
- Попытка изменить состояние компонента из background-потока вызывает исключение
- Нужно использовать `InvokeAsync` для маршалинга в Dispatcher

#### Blazor WebAssembly (WASM)

**Архитектура:**
- Компоненты выполняются **в браузере** через WebAssembly
- Нет серверного соединения (кроме HTTP-запросов)
- Работает в **single-threaded** среде браузера

**Модель потоков:**
- **Нет настоящих потоков** (пока)
- `Thread.Sleep` блокирует весь UI
- `Task.Delay` работает через JS timers (не блокирует)
- `InvokeAsync` не нужен (всё на одном потоке)

```
[Browser]
    ↓
[WebAssembly Runtime]
    ↓
[Blazor Components]
```

**Ограничения WASM:**
- Нет многопоточности (Web Workers есть, но не интегрированы)
- `Parallel.For` выполняется последовательно
- `Environment.ProcessorCount` может врать

#### Сравнение

| Аспект            | Blazor Server                 | Blazor WASM           |
|-------------------|-------------------------------|-----------------------|
| Где выполняется   | Сервер                        | Браузер               |
| Потоки            | Многопоточность (ThreadPool)  | Single-threaded       |
| InvokeAsync       | Нужен для background-потоков  | Не нужен              |
| Task.Delay        | Работает (ThreadPool timer)   | Работает (JS timer)   |
| Thread.Sleep      | Блокирует только один поток   | Блокирует весь UI     |
| JSInterop         | Через SignalR (медленнее)     | Напрямую (быстрее)    |
| Latency           | Зависит от сети               | Локально              |

### 10.2. InvokeAsync / Dispatcher

**Проблема:** Background-поток пытается изменить состояние компонента.

```csharp
// ПЛОХО — исключение
private Timer _timer;

protected override void OnInitialized()
{
    _timer = new Timer(_ =>
    {
        _counter++; // исключение: "The current thread is not associated with the Dispatcher's synchronization context"
        StateHasChanged(); // исключение
    }, null, 0, 1000);
}
```

**Решение:** Использовать `InvokeAsync` для маршалинга в Dispatcher.

```csharp
// ХОРОШО
private Timer _timer;

protected override void OnInitialized()
{
    _timer = new Timer(_ =>
    {
        InvokeAsync(() =>
        {
            _counter++;
            StateHasChanged();
        });
    }, null, 0, 1000);
}

public void Dispose()
{
    _timer?.Dispose();
}
```

**Как работает InvokeAsync:**
1. Background-поток вызывает `InvokeAsync`
2. Dispatcher ставит делегат в очередь
3. Dispatcher выполняет делегат в своём контексте (последовательно)
4. `StateHasChanged` вызывается в правильном контексте

**Полный пример:**

```csharp
@implements IDisposable

<div>Tick count: @_tickCount</div>
<button @onclick="StartTimer">Start</button>

@code {
    private Timer? _timer;
    private int _tickCount;

    private void StartTimer()
    {
        _timer = new Timer(_ =>
        {
            InvokeAsync(() =>
            {
                _tickCount++;
                StateHasChanged();
            });
        }, null, 0, 1000);
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
```

#### Демо

**Демо (консоль):** `12_BlazorAsyncSimulation.cs` — эмуляция Dispatcher/InvokeAsync через SynchronizationContext

**Демо (Blazor Server):** `BlazorServerDemo/Components/Pages/InvokeAsyncDemo.razor` — Timer → ThreadPool → InvokeAsync

### 10.3. JSInterop и асинхронность

**JSInterop** — механизм вызова JavaScript из C# и наоборот.

#### IJSRuntime.InvokeAsync<T>()

**Всегда async** — потому что пересечение границы JS/.NET требует сериализации.

```csharp
@inject IJSRuntime JS

<button @onclick="ShowAlert">Show Alert</button>

@code {
    private async Task ShowAlert()
    {
        await JS.InvokeVoidAsync("alert", "Hello from Blazor!");
    }
}
```

**InvokeVoidAsync** — для методов без возвращаемого значения:

```csharp
await JS.InvokeVoidAsync("console.log", "Message", DateTime.Now);
```

**InvokeAsync<T>** — для методов с возвращаемым значением:

```csharp
string name = await JS.InvokeAsync<string>("prompt", "Enter your name:");
bool confirmed = await JS.InvokeAsync<bool>("confirm", "Are you sure?");
```

#### IJSObjectReference — disposable references

Для модулей JS и объектов, которые нужно освобождать:

```csharp
await using var module = await JS.InvokeAsync<IJSObjectReference>(
    "import", "./scripts/helper.js"
);

await module.InvokeVoidAsync("helperFunction");
// module автоматически освобождается при выходе из scope
```

#### Callback из JS в C#

**DotNetObjectReference** — обёртка для передачи C# объекта в JS:

```csharp
@implements IDisposable

<button @onclick="CallJsCallback">Call JS, then C#</button>

@code {
    private DotNetObjectReference<MyComponent>? _objRef;

    private async Task CallJsCallback()
    {
        _objRef = DotNetObjectReference.Create(this);
        await JS.InvokeVoidAsync("jsFunction", _objRef);
    }

    [JSInvokable]
    public string GetServerTime()
    {
        return DateTime.Now.ToString("HH:mm:ss");
    }

    public void Dispose()
    {
        _objRef?.Dispose();
    }
}
```

**JavaScript:**

```javascript
window.jsFunction = (dotNetRef) => {
    dotNetRef.invokeMethodAsync('GetServerTime')
        .then(result => console.log(result));
};
```

**[JSInvokable]** — атрибут, который делает метод доступным из JS:
- Должен быть `public`
- Может быть `static` или instance
- Возвращает `T` или `Task<T>`

#### Демо

**Демо (Blazor Server):** `BlazorServerDemo/Components/Pages/JsInteropDemo.razor` — IJSRuntime + [JSInvokable]

**Демо (Blazor WASM):** `BlazorWasmDemo/Pages/JsInteropDemo.razor` — JSInterop в WASM

### 10.4. Lifecycle и async

Blazor имеет несколько lifecycle-методов, которые могут быть async.

#### OnInitializedAsync

**Важно:** В Blazor Server **первый render происходит ДО завершения** `OnInitializedAsync`.

```csharp
@code {
    private string? _data;

    protected override async Task OnInitializedAsync()
    {
        // Первый render происходит СЕЙЧАС (с _data = null)
        // UI показывает "Loading..."
        
        await Task.Delay(3000); // имитация загрузки
        
        _data = "Loaded data";
        // Второй render происходит ПОСЛЕ завершения
        // UI показывает данные
    }
}
```

**Почему так:**
- Blazor Server хочет быстро показать UI
- Первый render с пустыми данными происходит синхронно
- После завершения async-работы происходит второй render

**Паттерн loading state:**

```csharp
@code {
    private string? _data;
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _data = await LoadDataAsync();
        }
        finally
        {
            _loading = false;
        }
    }
}

@if (_loading)
{
    <p>Loading...</p>
}
else
{
    <p>Data: @_data</p>
}
```

#### OnAfterRenderAsync

Вызывается **после каждого render**. Можно безопасно работать с JS.

```csharp
@code {
    private bool _firstRender = true;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JS.InvokeVoidAsync("initializeChart");
        }
    }
}
```

**Когда использовать:**
- Инициализация JS-библиотек
- Работа с DOM (через JSInterop)
- Подписка на JS-события

#### StateHasChanged

**Автоматически вызывается после:**
- Lifecycle-методов (`OnInitializedAsync`, `OnParametersSetAsync`)
- Event handlers (`@onclick`)

**Нужно вызывать вручную, когда:**
- Background-работа обновляет состояние
- Используется `InvokeAsync` из другого потока
- Внешние события (Timer, SignalR) изменяют состояние

```csharp
@code {
    private int _counter;

    private async Task StartBackgroundWork()
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(1000);
                await InvokeAsync(() =>
                {
                    _counter++;
                    StateHasChanged(); // нужно вручную
                });
            }
        });
    }
}
```

**Pitfall:** `StateHasChanged` из background-потока без `InvokeAsync` вызывает исключение.

```csharp
// ПЛОХО
_ = Task.Run(() =>
{
    _counter++;
    StateHasChanged(); // исключение!
});

// ХОРОШО
_ = Task.Run(async () =>
{
    _counter++;
    await InvokeAsync(StateHasChanged);
});
```

#### Демо

**Демо (Blazor Server):** `BlazorServerDemo/Components/Pages/LifecycleDemo.razor` — OnInitializedAsync timing

---

## Заключение

В этом докладе мы рассмотрели:

1. **Фундамент** — потоки, ThreadPool, параллелизм
2. **Эволюцию** — от callback hell к async/await
3. **Task** — основной инструмент асинхронности
4. **Deadlock** — как избежать и как исправить
5. **async void** — почему это опасно
6. **ValueTask** — оптимизация для hot path
7. **Примитивы синхронизации** — lock, Mutex, Semaphore, volatile, Interlocked
8. **ThreadLocal vs AsyncLocal** — когда что использовать
9. **Dictionary vs ConcurrentDictionary** — thread-safe коллекции
10. **Blazor** — асинхронность в UI-фреймворке

**Ключевые правила:**
- Используйте `async/await` вместо callback
- Не блокируйте async-код (`async all the way`)
- В библиотеках используйте `ConfigureAwait(false)`
- `async void` только для event handlers
- Для thread-safe — `ConcurrentDictionary`, `lock`, `Interlocked`
- Для async-контекста — `AsyncLocal`, не `ThreadLocal`
- В Blazor Server — `InvokeAsync` для background-потоков

**Демо-проекты:**
- `AsyncMultithreadDemo` — консольное приложение с 12 демо
- `MutexApp.Second` — второе приложение для Mutex-демо
- `BlazorServerDemo` — Blazor Server с InvokeAsync, JSInterop, Lifecycle
- `BlazorWasmDemo` — Blazor WASM с JSInterop, single-threaded demo

Все демо можно запустить и поэкспериментировать с кодом.

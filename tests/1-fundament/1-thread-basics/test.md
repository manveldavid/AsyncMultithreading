# Задача: Система мониторинга серверов

## Условие

Вы разрабатываете консольное приложение для мониторинга здоровья серверов в дата-центре. Система должна одновременно опрашивать несколько серверов и ждать их ответа. Но есть нюанс: некоторые проверки являются **критическими** (foreground), а некоторые — **фоновыми** (background).

### Требования

1. **Создайте класс `ServerMonitor`**, который:
   - Принимает список серверов (название + IP)
   - Имеет метод `RunAllChecksAsync()` — запускает проверки и возвращает управление
   - Имеет метод `RunCriticalChecks()` — блокирует вызывающий код до завершения ВСЕХ критических проверок

2. **Реализуйте два типа проверок:**
   - **Критическая проверка (foreground-поток)** — эмулирует ping сервера (Thread.Sleep на 2-4 секунды). Процесс НЕ должен завершиться, пока все foreground-потоки не отработают. Каждый foreground-поток должен вывести: `[CRITICAL] Checking {server}... Done — {time}ms`
   - **Фоновая проверка (background-поток)** — отправляет метрики сервера на центральный узел (Thread.Sleep на 5-7 секунд). Выводит: `[BACKGROUND] Sending metrics for {server}... Done`. Процесс может завершиться, не дожидаясь их.

3. **Метод `RunCriticalChecks()`** должен:
   - Запускать ВСЕ критические проверки
   - Использовать `Join()` для ожидания завершения каждого foreground-потока
   - После завершения всех критических проверок вывести `All critical checks passed. Uptime: 100%`
   - Измерить общее время ожидания и показать его

4. **Метод `RunAllChecksAsync()`** должен:
   - Запустить все проверки (и критические, и фоновые)
   - Сразу вернуть управление в `Main` (не блокируясь)
   - Не мешать завершению процесса по завершении критических проверок (фоновые могут не успеть)

5. **Продемонстрируйте разницу между Foreground и Background:**
   - Сценарий A: Вызовите `RunAllChecksAsync()`, затем сразу `RunCriticalChecks()` — вы должны увидеть, что foreground-потоки из первого вызова ещё работают, и `Join()` во втором вызове их дождётся
   - Сценарий B: Вызовите `RunAllChecksAsync()`, дайте процессу завершиться — background-потоки НЕ должны успеть завершиться (показать, что их вывод отсутствует)

6. **Добавьте `ManagedThreadId`** в вывод каждого потока, чтобы было видно, на каких потоках ОС выполняется работа.

### Серверы (входные данные)

```csharp
var servers = new List<(string Name, string Ip)>
{
    ("Auth-DB",     "10.0.1.1"),
    ("Cache-Redis", "10.0.1.2"),
    ("API-Gateway", "10.0.1.3"),
    ("Worker-1",    "10.0.1.4"),
    ("Worker-2",    "10.0.1.5"),
};
```

### Ожидаемый вывод (пример)

```
[CRITICAL] Checking Auth-DB on thread 3... Done — 2150ms
[CRITICAL] Checking Cache-Redis on thread 4... Done — 3120ms
[BACKGROUND] Sending metrics for Auth-DB on thread 5...
[BACKGROUND] Sending metrics for Cache-Redis on thread 6...
[CRITICAL] Checking API-Gateway on thread 7... Done — 2800ms
...
All critical checks passed. Total wait time: 3120ms
Uptime: 100%
[BACKGROUND] Sending metrics for Auth-DB on thread 5... Done
Process finished. (background threads may not complete)
```

### Ограничения
- Минимум 5 серверов
- Foreground-потоки: все 5 серверов
- Background-потоки: только первые 3 сервера
- Не использовать `async/await` и `Task` — только `Thread`
- Для задержки использовать `Thread.Sleep` и `Random`

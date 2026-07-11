# Задача: Миграция legacy-сервиса на async/await

## Условие

Вы устроились в компанию, где есть старый сервис `LegacyFileService`. Он написан в трёх разных стилях асинхронности: **APM** (Begin/End), **EAP** (события) и синхронный код. Ваша задача — рефакторинг: переписать всё на современный **TAP** (async/await) и показать преимущества.

### Требования

1. **Реализуйте `LegacyFileService` с тремя методами (старый код):**

   **Метод `ReadFileApm(string path)`** — в стиле APM (BeginRead/EndRead):
   - Создаёт `FileStream`, читает 1024 байта через `BeginRead`/`EndRead`
   - Выводит прочитанный текст и размер
   - Callback-стиль — передаёт `AsyncCallback`, который вызывает `EndRead`
   - Демонстрирует «callback hell»: после первого чтения запускает второе чтение ВНУТРИ callback-а

   **Метод `DownloadDataEap(string url)`** — в стиле EAP (события):
   - Использует `WebClient.DownloadStringCompleted` событие
   - Выводит длину полученной строки или ошибку
   - Демонстрирует разбросанную логику: вызов метода и обработчик события в разных местах

   **Метод `ProcessFilesSync(List<string> paths)`** — синхронный:
   - Читает все файлы последовательно через `File.ReadAllText`
   - Считает общее количество строк во всех файлах
   - Замерить время выполнения

2. **Реализуйте `ModernFileService` — рефакторинг на TAP (async/await):**

   **Метод `ReadFileTapAsync(string path)`** — эквивалент `ReadFileApm` на async/await:
   - Использует `await fileStream.ReadAsync()`
   - Два чтения последовательно — без вложенности callback-ов
   - Обработка ошибок через try/catch

   **Метод `DownloadDataTapAsync(string url)`** — эквивалент `DownloadDataEap`:
   - Использует `await httpClient.GetStringAsync(url)`
   - Вся логика в одном методе (нет разброса)

   **Метод `ProcessFilesTapAsync(List<string> paths)`** — асинхронное чтение всех файлов:
   - Использует `await file.ReadAllTextAsync(path)` для каждого файла
   - Параллельная версия: `Task.WhenAll` для одновременного чтения ВСЕХ файлов
   - Сравнение: последовательная async vs параллельная async

3. **Метод `RunComparison()`** — демонстрирует эволюцию:
   - Запускает APM-версию
   - Запускает EAP-версию
   - Запускает TAP последовательную и параллельную версии
   - Выводит сравнительную таблицу времени и читаемости

4. **Создайте тестовые файлы:** сгенерируйте 5 текстовых файлов по ~1000 строк каждый во временной папке.

### Ожидаемый вывод

```
=== APM (BeginRead/EndRead) ===
[APM] Read 1024 bytes from file1.txt: "First 1024 chars..."
[APM] Callback hell: second read inside first callback — 1024 bytes
[APM] Done in 45ms

=== EAP (WebClient events) ===
[EAP] Downloaded 12345 chars from http://example.com/api/data
[EAP] Logic scattered: call in one place, handler in another
[EAP] Done in 230ms

=== TAP — Sequential async ===
[TAP-Seq] Read file1.txt: 1024 bytes
[TAP-Seq] Read file1.txt (2nd): 1024 bytes — no callback nesting!
[TAP-Seq] All files processed: 5000 lines in 120ms

=== TAP — Parallel async (WhenAll) ===
[TAP-Par] Reading 5 files in parallel...
[TAP-Par] All files processed: 5000 lines in 35ms (speedup: 3.4x)

=== COMPARISON ===
| Era  | Style         | Time   | Readability | Error handling |
|------|---------------|--------|-------------|----------------|
| APM  | Callback      | 45ms   | Terrible    | Manual         |
| EAP  | Events        | 230ms  | Bad         | Scattered      |
| TAP  | async/await   | 35ms   | Excellent   | try/catch      |
```

### Ограничения
- APM: использовать ТОЛЬКО `FileStream.BeginRead`/`EndRead`, без async/await
- EAP: использовать `WebClient` (не `HttpClient`), подписаться на `DownloadStringCompleted`
- TAP: использовать `HttpClient`, `File.ReadAllTextAsync`, `await`, `Task.WhenAll`
- Тестовый URL для EAP и TAP: можно использовать `https://jsonplaceholder.typicode.com/posts` или мок-сервер

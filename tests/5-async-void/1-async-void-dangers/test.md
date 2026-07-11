# Задача: UI-приложение для загрузки изображений с демонстрацией async void danger

## Условие

Вы разрабатываете галерею изображений. Пользователь нажимает кнопку «Загрузить», и приложение должно скачать изображение по URL. Но есть подвох: коллега написал обработчик как `async void`, и теперь исключения «пропадают» — приложение падает без видимых причин. Ваша задача — найти проблему и показать разницу.

### Требования

1. **Класс `ImageGallery`** — эмулирует UI-приложение в консоли.

2. **Метод `BadEventHandler()`** — async void event handler (имитация кнопки):
   - `async void OnDownloadClick()` — сигнатура для event handler
   - Загружает изображение, парсит его, сохраняет
   - На одном из этапов бросает исключение `InvalidOperationException("Image corrupted")`
   - Показать: **исключение НЕ ловится** вызывающим кодом
   - Продемонстрировать: try/catch вокруг вызова `OnDownloadClick()` не ловит исключение
   - Показать: исключение улетает в `SynchronizationContext` (или `AppDomain.UnhandledException`)

3. **Метод `GoodEventHandler()`** — правильный подход:
   - `async Task OnDownloadClickAsync()` — возвращает Task
   - То же самое исключение
   - Показать: try/catch вокруг `await OnDownloadClickAsync()` ЛОВИТ исключение
   - Разница очевидна

4. **Метод `DemonstrateFireAndForget()`** — правильный fire-and-forget:
   - Показать три способа:
     - ПЛОХО: `async void FireAndForget()` — исключение теряется
     - ХОРОШО: `_ = DoWorkAsync()` — discard, исключение можно обработать через `ContinueWith`
     - ХОРОШО: `DoWorkAsync().ContinueWith(t => LogError(t.Exception))`
   - Показать, что discard + ContinueWith — безопасная альтернатива fire-and-forget

5. **Метод `CatchUnhandledException()`** — глобальный обработчик:
   - Подписаться на `AppDomain.CurrentDomain.UnhandledException`
   - Показать, куда «улетает» исключение из async void
   - Поймать его глобально и вывести

6. **Метод `CompareAsyncVoidVsTask()`** — прямое сравнение:
   - Создать `async void Method()` и `async Task Method()`
   - Вызвать оба с одним и тем же исключением
   - Показать: `async Task` — try/catch работает, `async void` — нет
   - Показать: `async void` нельзя await — ошибка компиляции

### Ожидаемый вывод

```
=== Catching Unhandled Exceptions ===
[UnhandledException handler] registered

=== BAD: async void event handler ===
try {
    OnDownloadClick(); // async void — can't await!
} catch {
    // THIS CODE NEVER EXECUTES!
}
[UnhandledException] Caught: InvalidOperationException: Image corrupted
Exception escaped! The caller's try/catch is useless for async void.

=== GOOD: async Task handler ===
try {
    await OnDownloadClickAsync();
} catch (InvalidOperationException ex) {
    // THIS CODE EXECUTES!
    Caught: ex.Message → "Image corrupted"
}
Exception caught! async Task allows proper error handling.

=== Fire-and-forget patterns ===
BAD (async void):
  _ = DoWorkVoid(); // exception LOST

GOOD (discard + ContinueWith):
  DoWorkAsync().ContinueWith(t => {
      if (t.IsFaulted) Console.WriteLine($"Logged: {t.Exception.InnerException.Message}");
  });

=== Compilation errors with async void ===
// Cannot await 'void'
// Cannot catch exception from async void

Rule: async void ONLY for event handlers. async Task for everything else.
```

### Ограничения
- async void — только для event handler симуляции
- AppDomain.UnhandledException — показать, куда летит исключение
- ContinueWith для демонстрации правильного fire-and-forget
- Не использовать async void в других методах

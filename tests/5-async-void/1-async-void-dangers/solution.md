# Решение: UI-приложение для загрузки изображений с async void danger

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

Console.WriteLine("=== Catching Unhandled Exceptions ===\n");

AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    var ex = (Exception)e.ExceptionObject;
    Console.WriteLine($"[UnhandledException] Caught: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine("Exception escaped! The caller's try/catch is useless for async void.\n");
};

Console.WriteLine("[UnhandledException handler] registered\n");

var gallery = new ImageGallery();

Console.WriteLine("=== BAD: async void event handler ===\n");
gallery.BadEventHandler();

await Task.Delay(500);

Console.WriteLine("=== GOOD: async Task handler ===\n");
await gallery.GoodEventHandler();

Console.WriteLine("=== Fire-and-forget patterns ===\n");
gallery.DemonstrateFireAndForget();

await Task.Delay(500);

Console.WriteLine("\n=== Summary ===");
Console.WriteLine("Rule: async void ONLY for event handlers. async Task for everything else.");

public class ImageGallery
{
    public void BadEventHandler()
    {
        try
        {
            Console.WriteLine("try {");
            Console.WriteLine("    OnDownloadClick(); // async void — can't await!");
            OnDownloadClick();
            Console.WriteLine("} catch { // THIS CODE NEVER EXECUTES!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    Caught: {ex.Message}");
        }
    }

    private async void OnDownloadClick()
    {
        Console.WriteLine("    [async void] Downloading image...");
        await Task.Delay(100);
        Console.WriteLine("    [async void] Parsing image...");
        await Task.Delay(50);
        throw new InvalidOperationException("Image corrupted — exception thrown AFTER await");
    }

    public async Task GoodEventHandler()
    {
        try
        {
            Console.WriteLine("try {");
            Console.WriteLine("    await OnDownloadClickAsync();");
            await OnDownloadClickAsync();
            Console.WriteLine("} catch (InvalidOperationException ex) {");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"    Caught: {ex.GetType().Name}: \"{ex.Message}\"");
            Console.WriteLine("}");
            Console.WriteLine("Exception caught! async Task allows proper error handling.");
        }
    }

    private async Task OnDownloadClickAsync()
    {
        Console.WriteLine("    [async Task] Downloading image...");
        await Task.Delay(100);
        Console.WriteLine("    [async Task] Parsing image...");
        await Task.Delay(50);
        throw new InvalidOperationException("Image corrupted — exception thrown AFTER await");
    }

    public void DemonstrateFireAndForget()
    {
        Console.WriteLine("BAD (async void): exception LOST");
        _ = DoWorkVoid();

        Console.WriteLine("\nGOOD (discard + ContinueWith): exception LOGGED");
        DoWorkAsync().ContinueWith(t =>
        {
            if (t.IsFaulted)
                Console.WriteLine($"    Logged: {t.Exception?.InnerException?.Message}");
            else
                Console.WriteLine("    Completed successfully");
        });

        Console.WriteLine("\nGOOD (discard only):");
        _ = DoWorkAsync().ContinueWith(t =>
        {
            if (t.IsFaulted)
                Console.WriteLine($"    Logged via discard: {t.Exception?.InnerException?.Message}");
        });
    }

    private async void DoWorkVoid()
    {
        await Task.Delay(50);
        throw new InvalidOperationException("Lost in async void");
    }

    private async Task DoWorkAsync()
    {
        await Task.Delay(50);
        throw new InvalidOperationException("Caught by ContinueWith");
    }
}
```

## Ключевые моменты

1. **async void исключения не ловятся**: try/catch вокруг вызова `async void` метода бесполезен, если исключение бросается ПОСЛЕ первого `await`. Исключение улетает в `SynchronizationContext` или `AppDomain.UnhandledException` и роняет приложение.

2. **async Task исключения ловятся**: `await` + `async Task` позволяет использовать try/catch как обычно. Исключение привязывается к Task и доступно вызывающему коду.

3. **Нельзя await async void**: компилятор запрещает `await` на `void`. Невозможно дождаться завершения или обработать ошибку.

4. **Fire-and-forget**: вместо `async void` используйте `_ = DoWorkAsync()` (discard) с `ContinueWith` для логирования ошибок. Исключение не теряется.

5. **Единственное исключение**: `async void` допустим ТОЛЬКО для event handlers (button.Click, timer.Elapsed), потому что сигнатура события требует `void`.

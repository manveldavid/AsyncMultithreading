# Решение: Миграция legacy-сервиса на async/await

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

string testDir = Path.Combine(Path.GetTempPath(), "async_evo_test");
Directory.CreateDirectory(testDir);

Console.WriteLine("Generating test files...");
var paths = new List<string>();
for (int i = 1; i <= 5; i++)
{
    string path = Path.Combine(testDir, $"file{i}.txt");
    var sb = new StringBuilder();
    for (int j = 0; j < 1000; j++)
        sb.AppendLine($"File {i} — Line {j}: The quick brown fox jumps over the lazy dog. Padding text to make line longer for realistic file size.");
    File.WriteAllText(path, sb.ToString());
    paths.Add(path);
}
Console.WriteLine($"Generated 5 files with 1000 lines each in {testDir}\n");

// ========================================
// APM: BeginRead/EndRead (callback hell)
// ========================================
Console.WriteLine("=== APM (BeginRead/EndRead) ===\n");

var apmService = new LegacyFileService();
apmService.ReadFileApm(paths[0]);

// Wait for APM callbacks to complete
Thread.Sleep(500);

Console.WriteLine();

// ========================================
// EAP: WebClient events (scattered logic)
// ========================================
Console.WriteLine("=== EAP (WebClient events) ===\n");

var eapService = new LegacyFileService();
await eapService.DownloadDataEap();

Console.WriteLine();

// ========================================
// TAP: async/await (modern)
// ========================================
Console.WriteLine("=== TAP — async/await ===\n");

var tapService = new ModernFileService();

Console.WriteLine("--- Sequential async ---");
await tapService.ProcessFilesTapSequentialAsync(paths);

Console.WriteLine("\n--- Parallel async (WhenAll) ---");
await tapService.ProcessFilesTapParallelAsync(paths);

Console.WriteLine("\n--- Single file TAP ---");
await tapService.ReadFileTapAsync(paths[0]);

Console.WriteLine("\n--- HTTP download TAP ---");
await tapService.DownloadDataTapAsync();

// Cleanup
Directory.Delete(testDir, true);

public class LegacyFileService
{
    public void ReadFileApm(string path)
    {
        var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        var buffer = new byte[1024];
        var sw = Stopwatch.StartNew();

        fs.BeginRead(buffer, 0, buffer.Length, ar =>
        {
            int bytesRead = fs.EndRead(ar);
            string text = Encoding.UTF8.GetString(buffer, 0, bytesRead).Replace("\n", "\\n").Substring(0, Math.Min(60, bytesRead));
            Console.WriteLine($"[APM] Read {bytesRead} bytes: \"{text}...\"");

            // CABACK HELL: second read inside first callback
            var buffer2 = new byte[1024];
            fs.BeginRead(buffer2, 0, buffer2.Length, ar2 =>
            {
                int bytesRead2 = fs.EndRead(ar2);
                Console.WriteLine($"[APM] Callback hell: second read inside callback — {bytesRead2} bytes");
                sw.Stop();
                Console.WriteLine($"[APM] Done in {sw.ElapsedMilliseconds}ms");
                fs.Dispose();
            }, null);
        }, null);
    }

    public async Task DownloadDataEap()
    {
        var tcs = new TaskCompletionSource<(int length, string? error)>();

        using var client = new System.Net.WebClient();
        var sw = Stopwatch.StartNew();

        client.DownloadStringCompleted += (sender, e) =>
        {
            if (e.Error != null)
                tcs.SetResult((0, e.Error.Message));
            else if (e.Cancelled)
                tcs.SetResult((0, "Cancelled"));
            else
                tcs.SetResult((e.Result.Length, null));
        };

        // Using a public test API
        client.DownloadStringAsync(new Uri("https://jsonplaceholder.typicode.com/posts/1"));

        var (length, error) = await tcs.Task;

        if (error != null)
            Console.WriteLine($"[EAP] Error: {error}");
        else
            Console.WriteLine($"[EAP] Downloaded {length} chars");

        Console.WriteLine("[EAP] Logic scattered: call in one place, handler registered elsewhere");
        Console.WriteLine($"[EAP] Done in {sw.ElapsedMilliseconds}ms");
    }
}

public class ModernFileService
{
    public async Task ReadFileTapAsync(string path)
    {
        var sw = Stopwatch.StartNew();
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        var buffer = new byte[1024];

        int bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length);
        Console.WriteLine($"[TAP] Read {bytesRead} bytes — clean, flat code");

        int bytesRead2 = await fs.ReadAsync(buffer, 0, buffer.Length);
        Console.WriteLine($"[TAP] Second read: {bytesRead2} bytes — no callback nesting!");

        sw.Stop();
        Console.WriteLine($"[TAP] Done in {sw.ElapsedMilliseconds}ms");
    }

    public async Task DownloadDataTapAsync()
    {
        var sw = Stopwatch.StartNew();
        using var client = new HttpClient();

        try
        {
            string result = await client.GetStringAsync("https://jsonplaceholder.typicode.com/posts/1");
            Console.WriteLine($"[TAP] Downloaded {result.Length} chars — logic in ONE method");
            Console.WriteLine($"[TAP] Error handling: try/catch works naturally");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TAP] Caught: {ex.Message}");
        }

        Console.WriteLine($"[TAP] Done in {sw.ElapsedMilliseconds}ms");
    }

    public async Task ProcessFilesTapSequentialAsync(List<string> paths)
    {
        var sw = Stopwatch.StartNew();
        int totalLines = 0;

        foreach (string path in paths)
        {
            string content = await File.ReadAllTextAsync(path);
            int lines = content.Count(c => c == '\n');
            totalLines += lines;
            Console.WriteLine($"[TAP-Seq] {Path.GetFileName(path)}: {lines} lines");
        }

        sw.Stop();
        Console.WriteLine($"[TAP-Seq] Total: {totalLines} lines in {sw.ElapsedMilliseconds}ms");
    }

    public async Task ProcessFilesTapParallelAsync(List<string> paths)
    {
        var sw = Stopwatch.StartNew();

        var tasks = paths.Select(async path =>
        {
            string content = await File.ReadAllTextAsync(path);
            int lines = content.Count(c => c == '\n');
            Console.WriteLine($"[TAP-Par] {Path.GetFileName(path)}: {lines} lines");
            return lines;
        });

        int[] results = await Task.WhenAll(tasks);

        sw.Stop();
        Console.WriteLine($"[TAP-Par] Total: {results.Sum()} lines in {sw.ElapsedMilliseconds}ms");
    }
}
```

## Ключевые моменты

1. **APM → TAP**: `BeginRead/EndRead` с callback-ами превращается в `await stream.ReadAsync()`. Код становится линейным — никакого «callback hell».

2. **EAP → TAP**: События `DownloadStringCompleted` с разбросанной логикой заменяются на `await client.GetStringAsync()`. Вся логика в одном методе, try/catch работает как обычно.

3. **Sequential async vs Parallel async**: `Task.WhenAll` запускает чтение всех файлов одновременно. Для IO-bound задач это даёт прирост, потому что диски и сеть могут обрабатывать несколько операций параллельно.

4. **TAP — стандарт с C# 5**: Современный .NET построен на TAP. Все новые API возвращают `Task`/`Task<T>`. APM и EAP считаются устаревшими.

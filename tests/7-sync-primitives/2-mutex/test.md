# Задача: Single-instance приложение и межпроцессная синхронизация через Mutex

## Условие

Вы пишете десктопное приложение «Менеджер лицензий», которое должно запускаться **только в одном экземпляре**. Если пользователь пытается открыть второе окно — нужно показать сообщение и активировать уже запущенное. Также приложение должно защищать доступ к файлу лицензий от других процессов.

### Требования

1. **Класс `LicenseManager`** — должен гарантировать single-instance через именованный Mutex.

2. **Метод `TryStartApplication()`** — single-instance проверка:
   - Создаёт именованный Mutex: `"Global\\LicenseManagerApp"`
   - `new Mutex(true, name, out bool createdNew)`
   - Если `createdNew == true` — приложение запускается, выводит `License Manager started. PID: {pid}`
   - Если `createdNew == false` — выводит `Another instance is already running! Exiting.` и завершается
   - Удерживает mutex всё время работы приложения

3. **Метод `EditLicenseFile()`** — межпроцессная защита файла:
   - Создаёт локальный Mutex `"LicenseFileMutex"`
   - Захватывает его через `WaitOne()` (блокирующий вызов)
   - «Редактирует» файл лицензий (Thread.Sleep 5 секунд — эмуляция долгой операции)
   - Освобождает mutex через `ReleaseMutex()`
   - Если второй процесс пытается редактировать файл одновременно — он ЖДЁТ освобождения mutex
   - Показать: процесс A начал редактирование, процесс B ждёт, процесс A закончил → процесс B начинает

4. **Метод `DemonstrateAbandonedMutex()`** — показать AbandonedMutexException:
   - Симуляция: процесс захватывает mutex и «падает» (не вызывает ReleaseMutex)
   - Использовать отдельный процесс через `Process.Start` (или эмулировать через Task)
   - При следующем `WaitOne()` другой процесс получает `AbandonedMutexException`
   - Показать обработку: `catch (AbandonedMutexException) { ... }`
   - Вывести: «Mutex abandoned by previous process. Proceeding with caution.»

5. **Метод `SimulateMultiProcessEdit()`** — запуск нескольких «редакторов»:
   - Запустить 3 задачи, эмулирующие 3 разных процесса
   - Каждый пытается захватить `LicenseFileMutex`
   - Показать последовательный доступ: только один «процесс» в каждый момент времени
   - Вывести для каждого: `[Process {id}] Waiting for mutex...` → `Acquired!` → `Releasing...`

### Ожидаемый вывод

```
=== Single-instance check ===
License Manager started. PID: 12345
(If second instance tries: "Another instance is already running! Exiting.")

=== Inter-process file protection ===
[Process A] Waiting for LicenseFileMutex...
[Process A] Acquired! Editing license file...
[Process B] Waiting for LicenseFileMutex...
[Process B] (blocked — waiting for Process A to release)
[Process A] Editing done. Releasing mutex.
[Process B] Acquired! Editing license file...
[Process B] Editing done. Releasing mutex.

=== Abandoned Mutex ===
[Process C] Acquired mutex — simulating crash...
[Process C] CRASHED! (mutex abandoned)
[Process D] WaitOne() caught AbandonedMutexException!
Mutex abandoned by previous process. Proceeding with caution.
[Process D] We now own the mutex. State may be corrupt.
```

### Ограничения
- Использовать `Mutex` (не `MutexSlim`, не `lock`, не `Semaphore`)
- `WaitOne()` для блокирующего захвата
- `ReleaseMutex()` для освобождения
- `AbandonedMutexException` — отдельный сценарий
- Mutex должен корректно освобождаться через `using` или `try/finally`

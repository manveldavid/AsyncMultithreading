# Задача: Кэш товаров интернет-магазина — Dictionary vs ConcurrentDictionary benchmark

## Условие

Вы разрабатываете каталог товаров для интернет-магазина. Несколько потоков одновременно обновляют цены и наличие товаров. Обычный `Dictionary` падает с исключениями или теряет данные. Нужно сравнить `Dictionary + lock` и `ConcurrentDictionary` в разных сценариях нагрузки.

### Требования

1. **Класс `ProductCatalog`** с двумя реализациями: на Dictionary и на ConcurrentDictionary.

2. **Метод `DemonstrateDictionaryRaceCondition()`** — показать, как Dictionary ломается:
   - 10 потоков одновременно добавляют записи в обычный `Dictionary<int, Product>`
   - Каждый добавляет 10,000 записей
   - Результат: исключение (`ArgumentException: An item with the same key...`) или `Count` < 100,000
   - Вывести: итоговый Count и сколько записей потеряно

3. **Метод `FixWithLockedDictionary()`** — Dictionary + lock:
   - Тот же сценарий, но с `lock` вокруг каждой операции
   - Результат: Count = 100,000
   - Замерить время

4. **Метод `FixWithConcurrentDictionary()`** — ConcurrentDictionary:
   - Использовать `TryAdd` для добавления
   - Результат: Count = 100,000
   - Замерить время — сравнить с lock+Dictionary

5. **Метод `DemonstrateGetOrAddDoubleCall()`** — caveat с valueFactory:
   - Один ключ, 10 потоков одновременно вызывают `GetOrAdd(key, factory)`
   - Factory делает `Interlocked.Increment(ref counter)` + `Thread.Sleep(50)`
   - Показать: factory вызывается **больше одного раза** (несколько потоков видят отсутствие ключа)
   - Показать исправление через `ConcurrentDictionary + Lazy<T>` (вызывается ровно 1 раз)

6. **Метод `DemonstrateAddOrUpdate()`** — атомарное обновление:
   - 5 потоков обновляют цену товара через `AddOrUpdate`
   - Каждый пытается установить свою цену
   - Показать: `AddOrUpdate` с `updateValueFactory` — атомарная read-modify-write

7. **Метод `BenchmarkReadVsWrite()`** — сравнительный бенчмарк:
   - Сценарий Read-heavy: 90% чтений, 10% записей
   - Сценарий Write-heavy: 10% чтений, 90% записей
   - Для каждого сценария сравнить `lock+Dictionary` и `ConcurrentDictionary`
   - Вывести таблицу с временем и рекомендацией

### Ожидаемый вывод

```
=== Dictionary Race Condition ===
Dictionary Count: 87,234 (expected 100,000)
Lost: 12,766 entries. OR ArgumentException thrown!

=== Dictionary + lock ===
Count: 100,000
Time: 65ms

=== ConcurrentDictionary ===
Count: 100,000
Time: 48ms (faster due to lock striping)

=== GetOrAdd Double-Call Caveat ===
Factory called 3 times! (expected 1)
WARNING: GetOrAdd valueFactory may run multiple times under contention!

Fix with Lazy<T>:
Factory called 1 time. (correct!)

=== AddOrUpdate — Atomic Price Update ===
Product #42: initial price = $100
[Thread 3] Proposed $150 → Accepted
[Thread 5] Proposed $120 → Rejected (new price $150 > $120)
[Thread 7] Proposed $180 → Accepted
Final price: $180

=== Benchmark: Read-heavy vs Write-heavy ===
| Scenario    | Method              | Time  | Recommendation            |
|-------------|---------------------|-------|---------------------------|
| 90% reads   | lock + Dictionary   | 25ms  | Simple lock                |
| 90% reads   | ConcurrentDictionary| 28ms  | Slightly slower (overhead) |
| 10% reads   | lock + Dictionary   | 95ms  | Slow (contention)          |
| 10% reads   | ConcurrentDictionary| 42ms  | Fast (lock striping)       |
```

### Ограничения
- Для race condition — обычный `Dictionary` без lock (должен упасть)
- Для GetOrAdd double-call — использовать `Interlocked.Increment` внутри factory для подсчёта вызовов
- `Lazy<T>` для исправления double-call
- `AddOrUpdate` для атомарных обновлений

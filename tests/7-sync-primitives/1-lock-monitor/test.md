# Задача: Торговый движок криптобиржи — lock, race condition, deadlock

## Условие

Вы пишете торговый движок для криптобиржи. У каждого трейдера есть баланс, и они одновременно отправляют ордера на покупку/продажу. Без синхронизации балансы «теряются» (race condition). Нужно защитить данные через `lock`/`Monitor`, но аккуратно — вложенные lock-и ведут к deadlock.

### Требования

1. **Класс `ExchangeEngine`** — ядро биржи.

2. **Метод `DemonstrateRaceCondition()`** — показать race condition без синхронизации:
   - 10 трейдеров, каждый делает 10,000 депозитов по 1$
   - Общий баланс = 0 изначально
   - Без синхронизации: `balance += 1` из нескольких потоков
   - Результат: баланс < 100,000 (потеря данных!)
   - Показать, сколько потеряно в деньгах и процентах

3. **Метод `FixWithLock()`** — защита через `lock`:
   - Тот же сценарий, но с `lock (_balanceLock) { balance += 1; }`
   - Баланс = ровно 100,000
   - Замерить время и сравнить с версией без lock

4. **Метод `FixWithInterlocked()`** — lock-free альтернатива:
   - `Interlocked.Increment(ref balance)` вместо `lock`
   - Баланс = ровно 100,000
   - Замерить время — должно быть быстрее lock для простых операций

5. **Метод `DemonstrateDeadlockWithTwoLocks()`** — deadlock между трейдерами:
   - Два трейдера хотят обменяться: отправить BTC и получить ETH
   - Трейдер А: `lock(btcLock)` → `Thread.Sleep(50)` → `lock(ethLock)`
   - Трейдер Б: `lock(ethLock)` → `Thread.Sleep(50)` → `lock(btcLock)`
   - DEADLOCK: оба ждут друг друга
   - Показать таймаут через `Monitor.TryEnter`

6. **Метод `FixWithOrderedLocks()`** — всегда захватывать lock-и в одном порядке:
   - Сортировать lock-и по имени ресурса
   - Захватывать в алфавитном порядке: всегда сначала BTC, потом ETH
   - Deadlock не происходит

7. **Метод `CompareLockVsInterlockedVsNoSync()`** — сводный benchmark:
   - Без синхронизации: быстро, но неверный результат
   - С lock: правильно, но медленнее (monitor overhead)
   - С Interlocked: правильно и быстрее lock (lock-free)
   - Таблица сравнения

### Ожидаемый вывод

```
=== RACE CONDITION ===
Expected balance: $100,000
Actual balance:   $73,421
LOST: $26,579 (26.6%) — classic race condition!

=== FIXED WITH LOCK ===
Expected: $100,000
Actual:   $100,000
Time: 45ms — correct but with lock overhead

=== FIXED WITH INTERLOCKED ===
Expected: $100,000
Actual:   $100,000
Time: 12ms — lock-free, faster!

=== DEADLOCK WITH TWO LOCKS ===
[Trader A] Locked BTC, waiting for ETH...
[Trader B] Locked ETH, waiting for BTC...
Monitor.TryEnter timed out after 2000ms
<<< DEADLOCK DETECTED >>>

=== FIXED: LOCK ORDERING ===
[Trader A] Locked BTC → ETH — success
[Trader B] Locked ETH → BTC (BTC already locked by A, waiting...)
[Trader B] Got BTC — success
No deadlock — consistent lock order.

=== COMPARISON ===
| Method      | Result        | Time  | Thread-safe |
|-------------|---------------|-------|-------------|
| No sync     | $73,421       | 8ms   | NO          |
| lock        | $100,000      | 45ms  | YES         |
| Interlocked | $100,000      | 12ms  | YES         |
```

### Ограничения
- Не использовать `async/await` — только `Thread`, `lock`, `Monitor`, `Interlocked`
- Для race condition — минимум 10 потоков, 10,000 операций каждый
- Для deadlock — использовать `Monitor.TryEnter` с таймаутом для обнаружения
- Для упорядоченного захвата — принцип: lock-и всегда в одном порядке (например, по алфавиту)

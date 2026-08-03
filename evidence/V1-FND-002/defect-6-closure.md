# V1-FND-002 / V1-FND-006 — Kusur 6 Kapanış Kanıtı (2026-08-03)

Kapsam: EXECUTION_READY_PLAN.md Aşama 2, satır 6 — "Inbox/Outbox handler
DB lock/transaction açıkken çağrılıyor."

## Değişen yollar

| Dosya | Değişiklik |
|---|---|
| `src/BuildingBlocks/Messaging/OutboxStore.cs` | Lease modeli: `_leaseTimeout` ctor parametresi (varsayılan 5 dk); `DispatchAsync` claim'ı kendi transaction'ında; handler tamamen transaction dışında; per-message outcome transaction; `MarkDispatchedAsync` `WHERE id = $1 AND status = 'in_flight'` |
| `src/BuildingBlocks/Messaging/InboxStore.cs` | Aynı model: lease-recovery UPDATE → `ClaimPendingAsync` → `in_flight` lease; `MarkProcessedAsync`/`RecordFailureAsync` `status='in_flight'` guard |
| `src/BuildingBlocks/Messaging/RetryPolicy.cs` | `in_flight` guard |
| `src/BuildingBlocks/Messaging/InboxStatus.cs`, `OutboxStatus.cs` | `InFlight = 3` |
| `database/migrations/V1/V1-FND-002/002-inbox-messages.up.sql`, `003-outbox-messages.up.sql` | Lease şeması (`in_flight`, lease timeout sütunları) |
| `tests/BuildingBlocks/Idempotency/OutboxStoreTests.cs`, `InboxStoreTests.cs` | Yeni lease testleri |
| `tests/BuildingBlocks/TestHelpers/Fixtures/RecordingSink.cs` (varsa) | `RecordingSink`/`RecordingHandler` iki ctor (sync + async Func); `_ => throw` lambda'larına `(Func<..., Task<bool>>)` cast gerekti (CS0121) |
| `tests/BuildingBlocks/TransactionOutboxIntegration/TransactionOutboxIntegrationTests.cs` | Paralel worker senaryoları |

## Davranış

- Handler çağrısı DB transaction/lock açıkken yapılmaz; dispatch/process
  claim'ı kendi transaction'ında alınır, outcome ayrı transaction'da yazılır.
- Lease süresi dolan mesajlar yeni worker'larca tekrar claim edilebilir
  (lease-recovery), paralel worker'lar aynı mesajı çift işlemez.

## Test kanıtı

Komut (tam suite içinden):

```
Başarılı!  - Başarısız:     0, Başarılı:    69, Atlanan:     0, Toplam:    69, Süre: 2 s - ALKAROS.Idempotency.Tests.dll (net8.0)
Başarılı!  - Başarısız:     0, Başarılı:    12, Atlanan:     0, Toplam:    12, Süre: 1 s - ALKAROS.TransactionOutboxIntegration.Tests.dll (net8.0)
```

Idempotency 65 → 69 → 71 (Inbox/Outbox lease testleri + denetim fix'i: lease kaybı
senaryosu için 2 yeni test `DispatchLeaseLostBeforeMarkThrowsInsteadOfSkippingSilently`
ve `ProcessLeaseLostBeforeMarkThrowsInsteadOfSkippingSilently`), 
TransactionOutboxIntegration 11 → 12. Hepsi gerçek PostgreSQL container'ında
(alkaros_test:5433).

Denetim fix'i (2026-08-03): `MarkDispatchedAsync`/`MarkProcessedAsync` artık
etkilenen satır sayısını kontrol eder; `affected != 1` (lease kaybı) sessizce
geçilmek yerine `InvalidOperationException` fırlatır — mesaj yeniden claim
edileceği için teslim tekrarı görünür hale gelir (at-least-once davranışı
korunur).

Build: 0 Uyarı, 0 Hata, EXIT=0.

## Kapanış doğrulaması

- Kapanış ölçütü: "Claim/lease transaction içinde, handler dışında; paralel
  worker testleri geçer." — 69/69 + 12/12 geçti.
- Write-set yalnız yukarıdaki yollardadır. Commit bu kanıt dosyalarıyla birlikte push edildi (2026-08-03).

# V1-FND-002 — Kusur 3 Kapanış Kanıtı (2026-08-03)

Kapsam: EXECUTION_READY_PLAN.md Aşama 2, satır 3 — "Expired idempotency
kaydı aktif replay/conflict gibi kalabiliyor; replay TTL yeniliyor; sweep
production akışında yok."

## Değişen yollar

| Dosya | Değişiklik |
|---|---|
| `src/BuildingBlocks/Idempotency/IdempotencyKeyStore.cs` | Transaction + fast-path `INSERT ... ON CONFLICT DO NOTHING RETURNING` + yavaş yol `SELECT ... FOR UPDATE`; expired kayıt yerinde UPDATE ile yeni kayda dönüşür (Created + yeni envelope); aktif aynı hash → Replayed; farklı hash → rollback + `IdempotencyKeyConflictException`; concurrency `FOR UPDATE` ile serileşir |
| `tests/BuildingBlocks/Idempotency/IdempotencyKeyStoreTests.cs` | 5 yeni test + sweep testlerine `ResetTablesAsync()` |

## Teknik notlar (deneysel doğrulandı)

- `WITH ... DELETE` + `INSERT ... ON CONFLICT` tek ifadesi Postgres'te CTE'yi
  çalıştırmaz; no-op `DO UPDATE ... RETURNING (xmax = 0)` güvenilmez;
  Npgsql'de reader açıkken `CommitAsync` → `NpgsqlOperationInProgressException`.
- `request_hash` `CHAR(64)` → `GetString().TrimEnd()` gerektirir.

## Test kanıtı

Komut (tam suite içinden): Idempotency suite

```
Başarılı!  - Başarısız:     0, Başarılı:    69, Atlanan:     0, Toplam:    69, Süre: 2 s - ALKAROS.Idempotency.Tests.dll (net8.0)
```

Idempotency 65 → 69 test (5 yeni: expired-aktif-replay, expired-farklı-hash
conflict, concurrency, reset sonrası sweep). Hepsi gerçek PostgreSQL
container'ında (alkaros_test:5433).

Build: 0 Uyarı, 0 Hata, EXIT=0.

## Kapanış doğrulaması

- Kapanış ölçütü: "Expired key atomik yeni kayıt; active same-hash replay TTL
  korur; different-hash conflict; concurrency testi." — hepsi 69/69'da geçti.
- Write-set yalnız yukarıdaki yollardadır. Commit bu kanıt dosyalarıyla birlikte push edildi (2026-08-03).

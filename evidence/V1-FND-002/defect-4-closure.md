# V1-FND-002 / V0-GOV-015 — Kusur 4 Kapanış Kanıtı (2026-08-03)

Kapsam: EXECUTION_READY_PLAN.md Aşama 2, satır 4 — "`CREATE TABLE IF NOT
EXISTS`, uyumsuz şemayı kabul edip history'ye başarı yazabilir."

## Değişen yollar

| Dosya | Değişiklik |
| --- | --- |
| `src/Host/Composition/Migrations/MigrationHistoryStore.cs` | `EnsureAsync`: `CREATE TABLE IF NOT EXISTS` sonrası `information_schema.columns` + PK + CHECK constraint'leriyle fail-closed şema doğrulaması; uyumsuzlukta `InvalidOperationException` ("Migration history table schema does not match the expected contract.") |
| `tests/Host/MigrationComposition/History/MigrationHistoryTests.cs` | 2 yeni test: uyumsuz şema reddedilir, eşleşen şema kabul edilir |

## Davranış

- History tablosu yoksa beklendiği gibi oluşturulur.
- History tablosu VARSA ancak beklenen sütun/kısıtlamalarla uyuşmuyorsa
  migration devam etmez, history'ye başarı yazılmaz (fail-closed).

## Test kanıtı

Komut (tam suite içinden): Host suite

```console
Başarılı!  - Başarısız:     0, Başarılı:    60, Atlanan:     0, Toplam:    60, Süre: 13 s - ALKAROS.Host.Tests.dll (net8.0)
```

Host 55 → 57 → 60 test; 2 şema doğrulama testi dahil hepsi geçti.

Build: 0 Uyarı, 0 Hata, EXIT=0.

## Kapanış doğrulaması

- Kapanış ölçütü: "Schema doğrulaması fail-closed; uyumsuz şemada history
  yazılmaz." — 2/2 yeni test geçti.
- Write-set yalnız yukarıdaki yollardadır. Commit bu kanıt dosyalarıyla birlikte push edildi (2026-08-03).

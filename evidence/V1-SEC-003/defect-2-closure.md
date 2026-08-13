# V1-SEC-003 — Kusur 2 Kapanış Kanıtı (2026-08-03)

Kapsam: EXECUTION_READY_PLAN.md Aşama 2, satır 2 — "`--rollback` parse edilip
options'a aktarılmıyordu. `9a988a9` düzeltmeyi içerir; ProgramArgumentTests
senaryosu down script eksikliği yüzünden fail-closed validation'la çelişiyordu."

## Değişen yollar

| Dosya | Değişiklik |
| --- | --- |
| `src/Host/Program.cs` | `--rollback` argümanı composition options'a forward edilir (commit `9a988a9`, HEAD) |
| `tests/Host/MigrationComposition/Program/ProgramArgumentTests.cs` | Rollback forward testi eklendi; down script eksikliği senaryosu fail-closed validation ile tutarlı hale getirildi (2026-08-03 düzeltmesi) |

## Test kanıtı

Test `RollbackArgumentIsForwardedToTheHostComposition` — `--rollback 002`
argümanı parse edilir, composition'a iletilir ve fail-closed doğrulama mesajı
üretir:

```console
"Rollback refused: position [002] is not declared in the verified order."
```

Host suite (tam suite içinden):

```console
Başarılı!  - Başarısız:     0, Başarılı:    60, Atlanan:     0, Toplam:    60, Süre: 13 s - ALKAROS.Host.Tests.dll (net8.0)
```

Build: 0 Uyarı, 0 Hata, EXIT=0.

## Kapanış doğrulaması

- Kapanış ölçütü: "Program ve Host migration testleri forward path'e
  düşmediğini kanıtlar." — rollback testi fail-closed davranışı doğruluyor,
  forward path'e düşmüyor.
- Write-set yalnız yukarıdaki yollardadır. Commit bu kanıt dosyalarıyla birlikte push edildi (2026-08-03).

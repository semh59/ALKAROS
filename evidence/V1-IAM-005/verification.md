# V1-IAM-005 verification

Task: `V1-IAM-005` — login timing equality contract (FIND-IA-0056/0057, C42).
Date: 2026-08-04
Repo: `https://github.com/semh59/ALKAROS.git` branch `master` @ `78958da` (entry-gate onayı).

## Değişiklikler

- `docs/engineering/login-timing-contract.md` (yeni): yazılı güvenlik
  sözleşmesi — yol başına tam PBKDF2 doğrulama sayısı, DB yazım davranışı,
  work factor sınırları (10k–2M), `DummyHash` = `DefaultIterations` (600k),
  yakınsama politikası ve kanıt disiplini (stopwatch yasak).
- `src/Modules/Identity/Authentication/AuthenticationService.cs`: inject
  edilebilir `PasswordVerifier` delegate seam (varsayılan
  `PasswordHasher.Verify`); akış davranışı değişmedi, üretim yolu
  `PasswordHasher.Verify` olarak kalır.
- `tests/Modules/Identity/Authentication/AuthenticationServiceTests.cs`:
  `UnknownUsernameLoginTakesComparableTimeToKnownUserLogin` ve
  `InactiveUserLoginTakesComparableTimeToKnownUserLogin` (stopwatch + %80
  eşiği) kaldırıldı.
- `tests/Modules/Identity/Authentication/AuthenticationTimingContractTests.cs`
  (yeni): deterministik sözleşme testleri — sayan verifier + fake store ile
  her yolun tam doğrulama sayısı, DB yazımı, failure argümanları, dummy hash
  gerçekliği/iteration eşitliği ve work factor sınırları.

## Ortam

Test DB: `alkaros_test` docker container (host port 5433).
`ALKAROS_TEST_PG_PORT=5433`, `ALKAROS_TEST_PG_PASSWORD` container env'inden.

## Komutlar ve exit code'lar

### Hedefli koşu (ilk derleme hatası düzeltildi: CA1305 invariant culture)

```
> dotnet test ALKAROS.slnx --filter "FullyQualifiedName~Authentication"
Başarılı! - Başarısız: 0, Başarılı: 51, Toplam: 51 - ALKAROS.Identity.Authentication.Tests.dll
exit=0
```

### Tam çözüm — 3 ardışık koşu (kabul kanıtı)

```
> dotnet test ALKAROS.slnx --no-restore -v q   (run 1)
exit=0   (transcript: full-run-1.txt; Host 60/60, Architecture 5/5, Identity 51/51 dahil)

> dotnet test ALKAROS.slnx --no-restore -v q   (run 2)
exit=0   (transcript: full-run-2.txt; tüm projeler "Başarısız: 0")

> dotnet test ALKAROS.slnx --no-restore -v q   (run 3)
exit=0   (transcript: full-run-3.txt; Host 60/60 dahil, "Başarısız: 0")
```

Flake kontrolü: `tests/Modules/Identity/Authentication/**` içinde
`Stopwatch` kullanımı kalmadı (grep: 0 eşleşme).

### Kapsam ve plan doğrulaması

```
> py tools/task-scope/task_scope_tool.py --task-id V1-IAM-005 --format text
OK: All changes within scope for V1-IAM-005
exit=0

> py tools/plan-audit/plan_audit_tool.py validate
Validation errors: 0 | Validation warnings: 0
exit=0

> py tools/plan-audit/plan_audit_tool.py validate-coverage
Coverage errors: 0
exit=0

> py tools/plan-audit/plan_audit_tool.py verify-manifest
Manifest errors: 0
exit=0
```

## Kabul koşulları

- [x] Tam çözüm üç ardışık koşuda exit code 0 (flake'siz; stopwatch testi yok).
- [x] Sözleşme testleri deterministik geçer; eşik testi kaldırılmıştır.
- [x] Transcript `evidence/V1-IAM-005/**` altında kayıtlıdır.

## Kapanış notları

- Son kapsam kontrolü `Status: InProgress` iken alındı
  (`OK: All changes within scope for V1-IAM-005`, exit 0); araç yalnız
  `Planned`/`InProgress` durumunu doğrular (`Done` sonrası çalıştırılamaz).
- `plan/AUDIT_REPORT.md` + `plan/AUDIT_MANIFEST.json` yalnız araç yeniden
  üretimidir; nominal sahipler `V1-FND-008`/`V0-GOV-030`'dur. FIND-IA-0046 ve
  V0-GOV-031 (`78958da`) emsaliyle değişiklik geri alınmadı, kayıt düşüldü.
- Test DB erişimi `ALKAROS_TEST_PG_PORT=5433` + container parolası ile
  sağlanır (docker `alkaros_test`; Windows Postgres servisi durmuş, 5432
  aktif değil).

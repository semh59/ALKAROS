# V1-FND-013 verification

Task: `V1-FND-013` — prove host composition fail-closed constructability
(PDF:I.7-I.10, C42).
Date: 2026-08-04
Repo: `https://github.com/semh59/ALKAROS.git` branch `master`.

## Değişiklikler

- `src/Host/Composition/HostComposition.cs`: `BuildServiceProvider()` çağrısı
  `ValidateOnBuild = true, ValidateScopes = true` ile kuruldu; DI'nın
  constructability denetimi artık build sırasında çalışır. `ValidateOnBuild`
  başarısızlığı `AggregateException` (içi `InvalidOperationException`) olarak
  geldiği için catch filtresine
  `AggregateException { InnerException: InvalidOperationException }` eklendi —
  kırık graph `ComposeModules` → `null` → `StartupFailed` ile fail-closed
  reddedilir, migration öncesi durur.
- `tests/Host/MigrationComposition/Composition/HostConstructabilityTests.cs`
  (yeni): zincirli ctor graph'ı (leaf→mid→root, 3 kayıt) için build-time
  doğrulama + tüm servislerin `GetRequiredService` ile construct edilebilirliği;
  kırık graph modülü için `null` + "Module composition failed:" + kırık tip
  adının çıktıda bulunması.

## Komutlar ve exit code'lar

### Hedefli koşu

```console
> dotnet test ALKAROS.slnx --filter "FullyQualifiedName~HostConstructability"
Başarılı! - Başarısız: 0, Başarılı: 2, Toplam: 2 - ALKAROS.Host.Tests.dll
exit=0
```

İlk koşuda `AggregateException` escape etti (DI, bare
`InvalidOperationException` değil `AggregateException` fırlatır); catch
filtresi genişletilip yeniden koşuldu → 2/2 geçti.

### Tam çözüm — 3 ardışık koşu

```console
> dotnet test ALKAROS.slnx --no-restore -v q   (run 1,2,3)
run1=0  run2=0  run3=0
Transcripts: full-run-1.txt, full-run-2.txt, full-run-3.txt
Host.Tests: Başarısız: 0, Başarılı: 62 (60 eski + 2 yeni)
```

Test DB: `ALKAROS_TEST_PG_PORT=5433` + `alkaros_test` container parolası.

### Kapsam ve plan doğrulaması

```console
> py tools/task-scope/task_scope_tool.py --task-id V1-FND-013 --format text
OK: All changes within scope for V1-FND-013
exit=0

> py tools/plan-audit/plan_audit_tool.py validate            -> errors 0 (288 md, 267 task, 939 edge)
> py tools/plan-audit/plan_audit_tool.py validate-coverage   -> errors 0
> py tools/plan-audit/plan_audit_tool.py verify-manifest     -> errors 0 (402 md, added-file hashes 190)
```

## Kapanış notları

- `plan/AUDIT_REPORT.md` + `plan/AUDIT_MANIFEST.json`: yalnız araç yeniden
  üretimi (nominal sahipler V1-FND-008/V0-GOV-030); FIND-IA-0046 ve
  V0-GOV-031 emsaliyle geri alınmadı, kayıt düşüldü.
- Son kapsam kontrolü `InProgress` iken alındı; araç `Done` sonrası
  çalıştırılamaz.

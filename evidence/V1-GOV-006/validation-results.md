# V1-GOV-006 validation results

- Repository root: `D:/PROJECT/ALKAROS`
- Candidate commit: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`
- Task: `V1-GOV-006`
- Assignee: `/root/remediation_surface_custody`
- Result: `Done`

## Pre/post write-set separation

- Preflight dirty entry count: `81`
- Preflight canonical SHA-256: `2a2cf8a323ee5fc723cf8b6816756530a3ced224a31ce0dc47b0e396cc65eda5`
- Postflight allowlist dışı entry count: `78`
- Üç pre-existing ve bu görevde düzenlenen RMD task dosyasının preflight hash'i tekrar eklendiğinde reconstructed entry
  count: `81`
- Reconstructed canonical SHA-256:
  `2a2cf8a323ee5fc723cf8b6816756530a3ced224a31ce0dc47b0e396cc65eda5`
- Preflight eşleşmesi: `true`
- Sonuç: Başlangıçtaki allowlist dışı production/test/config/evidence dosyalarının path, status ve SHA-256 değerleri
  değişmedi. Ayrıntılı manifest `preflight-write-set.sha256` dosyasındadır.

V1-GOV-006'nın ilk `Blocked` kapanışından sonra V1-GOV-007 yalnız plan-custody dosyalarını değiştirmiştir. Continuation
preflight'inde RMD-006 SHA-256 değeri
`540bf7d972cdd9ab8c216a77eb913012ea42e1c0fff070c280c778261f5e392d`, RMD-009 SHA-256 değeri
`75afdb3c69f190f8d1e92dd1324aaaa53b1b2f4379ea299b5b4a0c7405bcf290` olarak kaydedildi ve V1-GOV-006
finalizasyonunda değişmedi. Production, test ve configuration path'ine yazılmadı.

`git status` her çalışmada erişilemeyen `.pytest_cache/` dizini için permission warning üretmiştir; bu dizin Git
write-set kaydında görünmez ve görev tarafından değiştirilmemiştir.

## Commands

### Plan validation

Command:

```text
python -B tools/plan-audit/plan_audit_tool.py validate
```

Exit code: `0`

```text
Markdown files: 402
Task files: 380
Registered gates: 18
Registered EXT sources: 21
Dependency edges: 1325
Validation errors: 0
Validation warnings: 0
```

V1-GOV-007 iki migration-composition path'ini V1-RMD-006 Owned surface'inden çıkarıp yalnız V1-RMD-009'da bıraktı.
CAT-002/FND-021 historical migration devirleri, RMD-004 WebPrototype devri ve RMD-007 reserved build exception'ında
overlap kalmadı.

### Task-scope validation

Command:

```text
python -B tools/task-scope/task_scope_tool.py --task-id V1-GOV-006 --repo-root . --format text
```

Exit code: `1`

Araç başlangıçtan kalan global dirty write-set, V1-GOV-007'nin ayrı custody artifact'leri ve henüz commit edilmemiş yeni
task Markdown dosyaları için allowlist dışı findings üretmiştir. Pre-existing dosyalar `preflight-write-set.sha256`,
sonraki custody değişiklikleri `evidence/V1-GOV-007/pre-post-sha256.txt` ile ayrılmıştır; task-scope sonucu başarı
olarak sunulmamıştır.

### Owned tracked diff whitespace check

Command:

```text
git diff --check -- plan/v1/governance/V1-GOV-006-remediation-surface-custody.md \
  plan/v1/remediation/V1-RMD-007-governance-manifest-and-build-provenance.md \
  plan/v1/remediation/V1-RMD-009-sql-persistence-concurrency-and-invariants.md \
  plan/v1/remediation/V1-RMD-011-web-prototype-hardening-and-touch-targets.md \
  plan/v1/remediation/V1-RMD-004-mock-runtime-contract-alignment.md \
  plan/v1/catalog/V1-CAT-002-effective-pricing.md \
  plan/v1/foundation/V1-FND-021-postgresql-extension-integration.md
```

Exit code: `0`

## Blocker resolution verification

- V1-GOV-007 status: `Done`; assignee: `/root/migration_manifest_custody`.
- `database/MigrationComposition/order.json` yalnız V1-RMD-009 Owned surface'inde bulunur.
- `tests/Host/MigrationComposition/Manifest/ManifestTests.cs` yalnız V1-RMD-009 Owned surface'inde bulunur.
- V1-RMD-006 `Blocked` kalmış, diğer blocker gerekçeleri ve davranış kapsamı korunmuştur.
- V1-RMD-009 `Planned` kalmış ve dependencies listesine V1-GOV-007 eklenmiştir.
- GOV-007 evidence'ındaki task-file postflight hash'i
  `254f07d7dcf73606d6e2acc394b668fe031e51da6a208a30315eae768358c87a`, task'ın `InProgress` metadata anına aittir.
  Güncel `Done` dosyasının hash'i `3692a3704b2a045b06b894cd54180b151ed1bd7ff5b79fa78e12c8df973bf4cb` olup yalnız
  `Status: InProgress` satırı `Status: Done` yapıldığında eski hash birebir yeniden üretilir.

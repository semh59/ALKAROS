# V1-GOV-007 - Migration manifest custody correction

- Task ID: V1-GOV-007
- Status: Done
- Assignee: /root/migration_manifest_custody
- Work type: validation
- Surface state: Existing

## Goal

PO:2026-08-24 tam production denetimi planındaki migration manifest ownership çakışmasını dar bir plan-custody
düzeltmesiyle gidermek. Bu görev production, test, migration veya configuration artifact'i değiştirmez.

## Owned surface

- `plan/v1/governance/V1-GOV-007-migration-manifest-custody.md`
- `plan/v1/remediation/V1-RMD-006-production-dual-screen-pos.md`
- `plan/v1/remediation/V1-RMD-009-sql-persistence-concurrency-and-invariants.md`
- `evidence/V1-GOV-007/**`

## In scope

- `database/MigrationComposition/order.json` ve
  `tests/Host/MigrationComposition/Manifest/ManifestTests.cs` exact path'lerini V1-RMD-006 Owned surface'inden
  çıkarmak ve V1-RMD-009'un tek remediation custody'sinde bırakmak.
- V1-RMD-006'nın mevcut `Blocked` durumunu ve blocker gerekçelerini korumak.
- V1-RMD-009'un `Planned` durumunu ve mevcut dependency zincirini koruyarak bu custody düzeltmesini dependency olarak
  kaydetmek.

## Out of scope

- Production, test, migration, build, CI veya configuration dosyalarını değiştirmek.
- V1-RMD-006 ya da V1-RMD-009'un hedef, davranış veya acceptance kapsamını genişletmek.
- Remediasyon uygulamak veya production readiness hükmü üretmek.

## Dependencies

- None

## Deliverables

- Migration manifesti ve manifest testinin çakışmasız tek sahipli remediation allowlist'i.
- Başlangıç kirli write-set'ini ayıran pre/post hash ve validation kanıtı.

## Acceptance evidence

- V1-RMD-006 `Blocked` kalır; iki exact migration-composition path'i artık Owned surface'inde bulunmaz ve custody
  correction notu V1-RMD-009'a devri açıklar.
- V1-RMD-009 `Planned` kalır; iki exact path'i Owned surface'inde tutar ve dependency listesi V1-GOV-006,
  V1-GOV-007 ve V1-RMD-008'i içerir.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code 0 verir.
- Başlangıç snapshot'ındaki allowlist dışı dosyaların pre/post SHA-256 değerleri değişmez; global task-scope mevcut
  kirli/untracked baseline nedeniyle başarısızsa bu sonuç fail-closed ve ayrıştırılmış olarak kaydedilir.

## Handoff

- V1-GOV-006

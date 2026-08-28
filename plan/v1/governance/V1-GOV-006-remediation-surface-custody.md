# V1-GOV-006 - Remediation surface custody

- Task ID: V1-GOV-006
- Status: Done
- Assignee: /root/remediation_surface_custody
- Work type: validation
- Surface state: Existing

## Goal

PO:2026-08-24 tam production denetimi planındaki build provenance, historical PostgreSQL lifecycle ve WebPrototype
remediasyonlarının exact write custody sınırlarını kurmak. Bu görev production, test veya configuration artifact'i
değiştirmez.

## Owned surface

- `plan/v1/governance/V1-GOV-006-remediation-surface-custody.md`
- `plan/v1/remediation/V1-RMD-007-governance-manifest-and-build-provenance.md`
- `plan/v1/remediation/V1-RMD-009-sql-persistence-concurrency-and-invariants.md`
- `plan/v1/remediation/V1-RMD-011-web-prototype-hardening-and-touch-targets.md`
- `plan/v1/remediation/V1-RMD-004-mock-runtime-contract-alignment.md`
- `plan/v1/catalog/V1-CAT-002-effective-pricing.md`
- `plan/v1/foundation/V1-FND-021-postgresql-extension-integration.md`
- `evidence/V1-GOV-006/**`

## In scope

- V1-RMD-007'ye yalnız `Directory.Build.props` için dar, tek seferlik reserved-surface integration yetkisi vermek.
- V1-RMD-009'a iki historical migration çifti, global order manifesti ve lifecycle/manifest testleri için exact
  correction custody vermek; `CASCADE` kullanımını açıkça reddetmek.
- V1-RMD-004'ün WebPrototype wildcard sahipliğini exact path'lerle V1-RMD-011'e devretmek.

## Out of scope

- Production, test, migration, build, CI veya configuration dosyalarını değiştirmek.
- V1-FND-001'in tarihsel body/handoff'unu ya da tarihsel görevlerin `Done` statülerini değiştirmek.
- Bu görevde remediasyon uygulamak veya dış kanıt üretmek.

## Dependencies

- None

## Deliverables

- Exact, çakışmasız RMD-007, RMD-009 ve RMD-011 write allowlist'leri.
- Tarihsel sahiplik devirlerini açıklayan dar custody notları.
- Başlangıç kirli write-set'ini SHA-256 ile ayıran ve pre/post değişimini doğrulayan kanıt paketi.

## Acceptance evidence

- V1-RMD-007 `integration` türündedir; mevcut exact yüzeylerine yalnız `Directory.Build.props` eklenir ve dependency
  listesi V1-GOV-006'yı içerir.
- V1-RMD-009 `Planned` durumundadır; exact 007/012 migration çiftleri, order manifesti ve iki lifecycle/manifest test
  dosyasını sahiplenir; dependency listesi V1-GOV-006 ve V1-RMD-008'dir; `CASCADE` kabul edilmez.
- V1-RMD-011 `Planned` durumundadır; mevcut yedi WebPrototype dosyasını exact path'lerle sahiplenir ve dependency
  listesi V1-GOV-006 ile V1-RMD-010'dur; V1-RMD-004 wildcard ownership bırakmaz.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code 0 verir.
- Başlangıç snapshot'ındaki allowlist dışı dosyaların pre/post SHA-256 değerleri değişmez; task-scope global kirli
  baseline nedeniyle başarısızsa bu sonuç fail-closed ve ayrıştırılmış olarak kaydedilir.

## Handoff

- V1-GOV-005

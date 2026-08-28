# V1-GOV-008 - Catalog API custody correction

- Task ID: V1-GOV-008
- Status: Done
- Assignee: /root/catalog_api_custody
- Work type: validation
- Surface state: Existing

## Goal

PO:2026-08-24 tam production denetimi planındaki katalog sayfalama sözleşmesinin production HTTP yüzeyi sahipliğini
tek bir remediasyon görevinde toplamak. Bu görev production, test veya configuration artifact'i değiştirmez.

## Owned surface

- `plan/v1/governance/V1-GOV-008-catalog-api-custody.md`
- `plan/v1/remediation/V1-RMD-008-host-api-auth-ratelimit-tls-and-telemetry.md`
- `plan/v1/remediation/V1-RMD-009-sql-persistence-concurrency-and-invariants.md`
- `evidence/V1-GOV-008/**`

## In scope

- `src/Host/DualScreen/DualScreenApplication.cs` ve
  `tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs` exact path'lerinin remediation custody'sini
  tamamlanmış V1-RMD-008'den Planned V1-RMD-009'a devretmek.
- V1-RMD-008'in tarihsel `Done` durumunu, uygulanan güvenlik davranışını ve diğer exact test sahipliğini korumak.
- V1-RMD-009 kabul kanıtına production HTTP üzerinden backward-compatible varsayılan katalog, kategori filtresi,
  deterministic cursor ve bounded limit doğrulamalarını eklemek.

## Out of scope

- Production, test, migration veya configuration dosyalarını değiştirmek.
- V1-RMD-008'in tamamlanmış güvenlik davranışını ya da durumunu yeniden açmak.
- V1-RMD-009'un katalog HTTP erişimi dışındaki hedef veya acceptance kapsamını değiştirmek.

## Dependencies

- V1-GOV-007
- V1-RMD-008

## Deliverables

- Katalog HTTP endpoint ve gerçek HTTP contract testinin çakışmasız tek sahipli remediation allowlist'i.
- Başlangıç kirli write-set'ini ayıran pre/post hash ve validation kanıtı.

## Acceptance evidence

- V1-RMD-008 `Done` kalır; iki exact katalog HTTP path'i Owned surface'inden çıkarılır ve narrow custody correction
  notu tarihsel davranışın korunduğunu açıklar.
- V1-RMD-009 `Planned` kalır; iki exact path'i Owned surface'ine alır ve dependency listesi V1-GOV-008'i içerir.
- V1-RMD-009 gerçek HTTP acceptance kapsamı backward-compatible varsayılan katalog, kategori filtresi, deterministic
  cursor ve bounded limit testlerini açıkça zorunlu kılar.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code 0 verir.
- Başlangıç snapshot'ındaki allowlist dışı dosyaların pre/post SHA-256 değerleri değişmez; global task-scope mevcut
  kirli/untracked baseline nedeniyle başarısızsa bu sonuç fail-closed ve ayrıştırılmış olarak kaydedilir.

## Handoff

- V1-RMD-009

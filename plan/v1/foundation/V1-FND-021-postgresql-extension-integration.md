# V1-FND-021 - Integrate the approved PostgreSQL extension lifecycle

- Task ID: V1-FND-021
- Status: Done
- Assignee: Antigravity-v1-fnd-021
- Work type: integration
- Surface state: Planned

## Source basis

- CORR:C52

## Goal

`V0-DAT-007` kararını yeni ve additive bir migration ile PostgreSQL 18 üzerinde uygulamak; `btree_gist` forward/reverse
residue davranışını eski Catalog migration'larını yeniden yazmadan kanıtlamak.

## Owned surface

- `evidence/V1-FND-021/**`

PO:2026-08-24 tam production denetimi correction custody kararıyla 012 migration çifti ve
`tests/Host/MigrationComposition/PostgresqlExtensionLifecycleTests.cs` yazma yetkisi exact path'lerle V1-RMD-009'a
devredildi; bu tarihsel görev `Done` kalır ve devir wildcard veya başka foundation yüzeyi yetkisi üretmez.

## In scope

- Boş DB, pre-existing extension ve ALKAROS-owned extension başlangıçlarını benzersiz PostgreSQL 18 veritabanlarında
  sınamak.
- Decision dedicated migration seçerse `012` migration çiftini oluşturmak;
  external/shared owner seçerse SQL dosyası üretmeden aynı lifecycle testlerini
  owner precondition'ına karşı çalıştırmak.

## Out of scope

- `V1-CAT-002` veya başka mevcut migration dosyasını ve `database/MigrationComposition/order.json` dosyasını
  değiştirmek.
- Decision record dışında extension owner/policy seçmek veya kullanıcı veritabanına dokunmak.

## Dependencies

- V0-GOV-035
- V0-DAT-007
- V1-FND-012

## Deliverables

- Decision sonucuna göre additive `012` migration çifti veya açık no-SQL
  artifact kararı; her durumda PostgreSQL 18 lifecycle testleri ve raw transcript.

## Acceptance evidence

- `V0-DAT-007` sonucu exact uygulanır ve task her owner modelinde runtime
  lifecycle kanıtıyla `Done` olur; `NotApplicable` kullanılmaz.
- Dedicated migration seçilmezse iki SQL yolunun bulunmadığı hash/path
  inventory ile kanıtlanır.
- Üç başlangıç durumunda forward/down/forward sonucu decision record ile birebir eşleşir.
- Mevcut Catalog migration hash'leri değişmez.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; kanıtlar yalnız `evidence/V1-FND-021/**`
  altındadır.

## Handoff

- V1-FND-022
- V1-RMD-009

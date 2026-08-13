# V1-FND-021 - Integrate the approved PostgreSQL extension lifecycle

- Task ID: V1-FND-021
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: integration
- Surface state: Planned

## Source basis

- CORR:C52

## Goal

`V0-DAT-007` kararını yeni ve additive bir migration ile PostgreSQL 18 üzerinde uygulamak; `btree_gist` forward/reverse
residue davranışını eski Catalog migration'larını yeniden yazmadan kanıtlamak.

## Owned surface

- `tests/Host/MigrationComposition/PostgresqlExtensionLifecycleTests.cs`
- `database/migrations/V1/V1-FND-021/012-btree-gist-ownership.up.sql`
- `database/migrations/V1/V1-FND-021/012-btree-gist-ownership.down.sql`
- `evidence/V1-FND-021/**`

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

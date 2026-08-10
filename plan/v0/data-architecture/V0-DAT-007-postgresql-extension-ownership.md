# V0-DAT-007 - Define PostgreSQL extension ownership and rollback policy

- Task ID: V0-DAT-007
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision
- Surface state: Planned

## Source basis

- CORR:C52

## Goal

`btree_gist` extension'ının tek sahibini ve forward/reverse lifecycle
sözleşmesini belirlemek; `V1-FND-021` uygulamasının politika varsaymasını
önlemek.

## Owned surface

- `docs/data/postgresql-extension-ownership.md`
- `evidence/V0-DAT-007/**`

## In scope

- Foundation migration, feature migration ve external/pre-provisioned owner
  seçeneklerinden tam olarak birini seçmek.
- Install privilege/precondition, forward lifecycle, reverse lifecycle,
  pre-existing extension davranışı ve kabul edilen residue'yu tanımlamak.
- Boş DB, pre-existing extension ve ALKAROS tarafından oluşturulan extension
  durumlarında rollback sonrası beklenen `pg_extension` durumunu yazmak.

## Out of scope

- SQL, migration, test, project/lockfile veya mevcut `V1-CAT-002` görev ve
  artifact yüzeylerini değiştirmek.
- PostgreSQL resmi davranışında doğrulanmayan fallback veya sahiplik varsaymak.

## Dependencies

- V0-GOV-035

## Deliverables

- `docs/data/postgresql-extension-ownership.md` yolunda tek decision record.
- Seçilen sonuç, reddedilen alternatifler, gerekçe, kaynak/erişim tarihi,
  named technical approver ve etkilenen `V1-CAT-002`, `V1-CAT-003`,
  `V1-FND-021` görevleri.

## Acceptance evidence

- Mevcut `CREATE EXTENSION IF NOT EXISTS btree_gist` davranışının tek sahibi
  belirlenir.
- Üç başlangıç durumu için forward/reverse sonrası `pg_extension` beklentisi
  ve doğrulama sorgusu kesin olarak yazılır.
- Drop edilmeyen extension shared, pre-existing veya irreversible residue
  sınıflarından biriyle gerekçelendirilir.
- `V1-FND-021` yeni politika seçmeden PostgreSQL 18 forward/reverse testini
  uygulayabilir.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir;
  kanıtlar yalnız `evidence/V0-DAT-007/**` altındadır.

## Handoff

- V1-FND-021

# V0-DAT-006 - Define migration rehearsal profile

- Task ID: V0-DAT-006
- Status: Done
- Assignee: codex-v0-dat-006
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:I.45.1
- PDF:III.39-III.40
- EXT:POSTGRESQL-18.4
- CORR:C17

## Goal

Migration rehearsal veri sınıflarını, sanitize kurallarını, hacim profilini ve control total sorgularını belirlemek.

## Owned surface

- `docs/data/migration-rehearsal-profile.md`
- Bu görev başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Dataset provenance, privacy, row-volume bands, financial/stock totals, invalid fixtures ve expected rejection.

## Out of scope

- Production data extraction, migration code ve rehearsal execution.

## Dependencies

- V0-DAT-001
- V0-DAT-002

## Deliverables

- Tek decision record: kaynaklar, erişim tarihleri, onaylayan, seçilen sonuç, reddedilen alternatifler ve etkilenen task
  kimlikleri.
- Seçilen dataset profile ve control query catalog.

## Acceptance evidence

- Her migration acceptance iddiası isimli dataset sınıfı ve beklenen control total ile ölçülebilir.

## Handoff

- V20-MIG-001
- V20-MIG-002

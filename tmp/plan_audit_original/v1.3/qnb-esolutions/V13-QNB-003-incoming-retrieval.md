# V13-QNB-003 - Implement QNB incoming invoice retrieval

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Ingest incoming provider documents once into dedicated immutable intake records.

## Owned surface

- `src/Modules/Invoicing/Qnb/Incoming/**`, `tests/Modules/Invoicing/Qnb/Incoming/**`, `database/migrations/V13/V13-QNB-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Cursor/checkpoint, provider ID uniqueness, validation state, raw-document protection and duplicate handling.

## Out of scope

- Supplier matching, goods receipt and payable posting.

## Dependencies

- V0-QNB-001,V0-CMP-003

## Deliverables

- V13-QNB-003 için production implementation.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Restart resumes from checkpoint; duplicate provider document creates one intake row; invalid document remains inspectable.

## Handoff

- V13-PUR-001 and V13-QNB-004.


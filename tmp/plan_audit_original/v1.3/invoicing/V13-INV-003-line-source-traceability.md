# V13-INV-003 - Implement invoice line to source traceability

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Map every invoice line to the exact account transaction set that produced it.

## Owned surface

- `src/Modules/Invoicing/SourceTraceability/**`, `tests/Modules/Invoicing/SourceTraceability/**`, `database/migrations/V13/V13-INV-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Typed source link, line/source join, uniqueness and retained audit path.

## Out of scope

- Invoice generation and provider status transport.

## Dependencies

- V13-INV-001,V13-INV-002

## Deliverables

- V13-INV-003 için production implementation.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Every line traces to one or more sources; every selected source is represented; orphan and duplicate links fail constraints.

## Handoff

- V13-QNB-002.


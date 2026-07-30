# V13-QNB-002 - Implement QNB outgoing invoice submission

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Submit one immutable invoice draft idempotently and persist provider references/status history.

## Owned surface

- `src/Modules/Invoicing/Qnb/Outgoing/**`, `tests/Modules/Invoicing/Qnb/Outgoing/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Payload mapping, idempotency key, accepted/rejected response, status query and sanitized raw evidence.

## Out of scope

- Incoming retrieval and customer balance calculation.

## Dependencies

- V13-QNB-001,V13-INV-002,V13-INV-003,V0-QNB-001

## Deliverables

- V13-QNB-002 için production implementation.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Same invoice retry creates one provider document; local reference matches sandbox evidence; rejected result is preserved.

## Handoff

- V13-QNB-004.


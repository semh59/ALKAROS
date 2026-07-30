# V13-INV-002 - Implement outgoing invoice generation

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Build invoice header and tax-grouped lines from the selected source set under the approved GIB/QNB profile.

## Owned surface

- `src/Modules/Invoicing/Generation/**`, `tests/Modules/Invoicing/Generation/**`, `database/migrations/V13/V13-INV-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- EInvoice/EArchive selection input, UBL-required identifiers, tax/rounding, immutable draft and customer snapshot.

## Out of scope

- QNB transport and registered-user lookup.

## Dependencies

- V13-INV-001,V13-CST-001,V0-CMP-001,V0-CMP-002

## Deliverables

- V13-INV-002 için production implementation.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Generated totals reconcile to source transactions and tax groups; generation does not add a second debit to account balance.

## Handoff

- V13-INV-003 and V13-QNB-002.


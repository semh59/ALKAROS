# V0-DOM-007 - Define customer credit and invoice reclassification semantics

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision

## Source basis

- PDF baseline plus the correction/gap named in this task; unsupported behavior requires business or external evidence.

## Goal

Define how deferred Bill charges, account payments and periodic invoice issuance affect receivable balance without double debit.

## Owned surface

- `docs/domain/customer-credit-invoice-semantics.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Charge, Payment, Invoice, Credit, Debit, Adjustment, Refund and invoice-cancellation signed effects.

## Out of scope

- Credit scoring, collection automation and general ledger integration.

## Dependencies

- V0-DOM-003,V0-CMP-002

## Deliverables

- V0-DOM-007 için bağlayıcı karar dokümanı.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- Example period with charges, payment, invoice and refund has one reproducible balance formula and no invoice double count.

## Handoff

- V13-ACC-001, V13-ACC-003 and V13-INV-001.


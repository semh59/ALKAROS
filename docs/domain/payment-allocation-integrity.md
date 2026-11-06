# Payment Allocation Integrity — approved decision record

> **Task:** V0-DOM-004
> **Status:** Done
> **Assignee:** codex-v0-dom-004
> **Work type:** decision
> **Source basis:** PDF:I.11-I.15, PDF:II.2.6, PDF:II.3.4-II.3.5,
> PDF:II.5.3, PDF:III.8, CORR:C4
> **Access date:** PDF source 2026-07-29; artifact verification 2026-08-02
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (PDF baseline + CORR:C4 + named approver)

## Selected model

Immutable `payment_allocations` rows. No negative allocation exists anywhere:

- `payment_allocations.id`, `payment_id`, `bill_id`, `amount > 0`, `currency`,
  `allocated_at`, all immutable after insert.
- A Payment belongs to exactly one Bill (`payment.bill_id`); every allocation
  row must satisfy `payment_allocations.bill_id = payment.bill_id`
  (same-bill invariant).
- Currency equality: `allocation.currency = payment.currency = bill.currency`;
  mismatch rejects the allocation.
- Remaining-amount invariant: at insert,
  `amount <= adjusted payable - sum(allocations of that bill)`, where the adjusted payable is the bill payable after
  its discounts and tips (`AdjustmentCalculator`); over-allocation rejects.
- Overpayment/change: only a cash tender may hand over more than the amount due; the surplus is not
  allocated. Card and transfer tenders above the remaining amount are rejected (`OverAllocationException`,
  `EftOverTenderException`). The surplus is recorded in a separate `payment.change_amount` field
  (`change_amount = tendered_amount - approved_amount`, constraint `ck_payments_change_amount_reconciles`,
  with refunds per V0-DOM-003). No compensating/negative allocation is ever written
  (CORR:C4).
- Idempotency: one `X-Idempotency-Key` per allocation; a duplicate replay
  returns the existing row instead of inserting (PDF:II.2.6).
- Refunds mutate nothing: `payment_reversals` from V0-DOM-003 carry the
  compensating semantics; Bill net paid =
  `sum(allocations) - sum(reversals)` and is never negative.

## Why

PDF:I.14.3 and PDF:II.3.4-II.3.5 separate paid amount from change; PDF:III.8
refund flow requires append-only records. The prior record's negative
compensating allocation contradicts `amount > 0` and is withdrawn (blocker).

## Examples

Positive 1 (overpayment with change): Bill payable 80; Payment 100 (cash) →
allocation row 80, `change_amount` = 20; Bill net paid = 80.

Positive 2 (multi-payment): Bill payable 80; Payment A 50 → allocation 50;
Payment B is a cash tender of 50 taken for the remaining 30 → allocation 30,
`change_amount` = 20; net paid = 80. Asking to allocate 50 against the
remaining 30 is rejected, never clipped.

Negative 1 (wrong bill): an allocation whose `bill_id` differs from
`payment.bill_id` → rejected.

Negative 2 (over-allocation / currency / replay): an allocation of 100 against
a bill with 80 remaining → rejected (the surplus belongs to `change_amount`,
never to the allocation); EUR payment against TRY bill →
rejected; duplicate idempotency-key insert → returns existing row, no second
row.

## Invariants for consumers

- `payment_allocations.bill_id = payment.bill_id` (same-bill).
- `allocation.currency = payment.currency = bill.currency`.
- `amount > 0`; `sum(allocations per bill) <= adjusted payable of the bill`.
- `change_amount = tendered_amount - approved_amount`; `change_amount >= 0`;
  no negative allocation (CORR:C4).
- Bill net paid = `sum(allocations) - sum(payment_reversals)` and is never
  negative (V0-DOM-003).
- One idempotency key per allocation; allocations immutable.

## Affected tasks

- Handoff: V13-ALC-001, V13-ALC-002.
- Consumers: V0-DOM-001 (Payment states), V0-DOM-003 (reversals),
  V13-PAY-001, V13-PAY-002, V1-BIL-001.

## Acceptance evidence

- Wrong-Bill allocation, over-allocation, different currency and duplicate
  replay each have explicit rejection rules (examples).
- Decision record with source, access dates, approver (Semih, 2026-08-03),
  selected result, rejected alternatives and affected task IDs.

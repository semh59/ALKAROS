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
  `amount <= bill.payable - sum(allocations of that bill)`; over-allocation
  rejects.
- Overpayment/change: every tender type (cash and card/meal-card alike) may
  receive more than the payable; the surplus is not allocated. It is recorded
  in a separate `payment.change_amount` field
  (`payment.amount = sum(allocations) + change_amount`, with refunds per
  V0-DOM-003). No compensating/negative allocation is ever written
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

Positive 2 (multi-payment cap): Bill payable 80; Payment A 50 → allocation
50; Payment B 50 → allocation capped at remaining 30, `change_amount` = 20;
net paid = 80.

Negative 1 (wrong bill): an allocation whose `bill_id` differs from
`payment.bill_id` → rejected.

Negative 2 (over-allocation / currency / replay): allocation 100 on a payable
80 bill without `change_amount` → rejected; EUR payment against TRY bill →
rejected; duplicate idempotency-key insert → returns existing row, no second
row.

## Invariants for consumers

- `payment_allocations.bill_id = payment.bill_id` (same-bill).
- `allocation.currency = payment.currency = bill.currency`.
- `amount > 0`; `sum(allocations per bill) <= bill.payable`.
- `payment.amount = sum(allocations) + change_amount`; `change_amount >= 0`;
  no negative allocation (CORR:C4).
- Bill net paid = `sum(allocations) - sum(payment_reversals)` and is never
  negative (V0-DOM-003).
- One idempotency key per allocation; allocations immutable.

## Affected tasks

- Handoff: V12-ALC-001, V12-ALC-002.
- Consumers: V0-DOM-001 (Payment states), V0-DOM-003 (reversals),
  V12-PAY-001, V12-PAY-002, V1-BIL-001.

## Acceptance evidence

- Wrong-Bill allocation, over-allocation, different currency and duplicate
  replay each have explicit rejection rules (examples).
- Decision record with source, access dates, approver (Semih, 2026-08-03),
  selected result, rejected alternatives and affected task IDs.

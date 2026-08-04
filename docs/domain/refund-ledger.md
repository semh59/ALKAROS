# Refund Ledger — approved decision record

> **Task:** V0-DOM-003
> **Status:** Done
> **Assignee:** codex-v0-dom-003
> **Work type:** decision
> **Source basis:** PDF:II.2.6, PDF:II.3.4-II.3.5, PDF:II.5.3, PDF:III.8
> **Access date:** PDF source 2026-07-29; artifact verification 2026-08-02
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (PDF baseline + named approver)

## Selected model

Immutable `payment_reversals` ledger. Existing payments and allocations are
never mutated; every refund appends a ledger row:

- `payment_reversals.id` surrogate key, immutable after insert.
- `payment_reversals.payment_id` → the original Payment.
- `reversal_type`: `FullRefund | PartialRefund`.
- `amount` > 0 always; no negative allocation representation anywhere.
- `reason`, `refunded_by`, `occurred_at`, fiscal linkage reference
  (FiscalDocument `Refunded`/refund request id per PDF:I.28.1).
- Provider idempotency: one `X-Idempotency-Key` per refund operation; a
  duplicate call returns the existing reversal row (PDF:II.2.6).

Payment state:

- Any refund appends a ledger row; a full refund transitions
  `Approved → Refunded`; a partial refund transitions `Approved →
  PartiallyRefunded`. The Payment does not stay `Approved` after a partial
  refund (approved 2026-08-03). `PartiallyRefunded` is part of the canonical
  Payment state set (V0-DOM-001).
- Subsequent partial refunds keep `PartiallyRefunded`; the final refund that
  brings cumulative refunded amount to the payment amount transitions
  `PartiallyRefunded → Refunded`.

Cumulative limits (invariants):

- `sum(payment_reversals.amount for payment) <= payment.amount` — over-refund
  is forbidden.
- Bill net paid = `sum(payments) - sum(reversals)`; never negative.
- A reversal row is immutable and cannot be reversed — double refund is
  forbidden by construction.
- Refund operations require the Payment in `Approved` or `PartiallyRefunded`;
  `Unknown`/`ReconciliationRequired` payments must be resolved first
  (V0-DOM-001 timeout rule).

## Why

PDF:II.5.3 supplies `Refunded` but no partial state; PDF:III.8 (refund flow)
and PDF:II.3.4-II.3.5 require partial amounts and provider/fiscal evidence.
A CHECK that rejects the partial row contradicts its own intent (prior
blocker); the ledger model keeps history immutable and the limit provable.

## Examples

Positive 1 (partial): Payment 100 → `payment_reversals(PartialRefund, 20)` →
Payment `PartiallyRefunded`; Bill net paid = 80. Acceptance example.

Positive 2 (full): Payment 100 → `payment_reversals(FullRefund, 100)` →
Payment `Refunded`; Bill net paid = 0; fiscal refund pathway triggered.

Negative 1 (over-refund): Payment 100, already reversed 20; a new reversal of
90 → sum 110 > 100 → rejected.

Negative 2 (double refund): `payment_reversals(FullRefund, 100)` already
exists; any new reversal row for the same payment → rejected; provider retry
returns the existing row via idempotency key.

## Invariants for consumers

- Existing payments/allocations are immutable; refunds are append-only rows.
- `amount` > 0; cumulative sum per payment ≤ payment amount; Bill net paid ≥ 0.
- Full refund → `Refunded`; partial → `PartiallyRefunded` (never stays
  `Approved`); no refund on `Unknown`/`ReconciliationRequired`.
- One idempotency key per refund operation.
- Fiscal refund/cancel linkage per PDF:I.28.1 (provider-specific to
  V12-HUG-003).

## Affected tasks

- Handoff: V12-ALC-003, V12-HUG-003.
- Consumers: V0-DOM-001 (PartiallyRefunded state), V12-PAY-002,
  V12-FSC-001 (fiscal refund), V1-BIL-001 (net paid computation).

## Acceptance evidence

- 100 paid / 20 refund → 80 net paid amount (positive 1).
- Double refund and over-refund prohibitions explicit (negatives 1-2).
- Decision record with source, access dates, approver (Semih, 2026-08-03),
  selected result, rejected alternatives and affected task IDs.

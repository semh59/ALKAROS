# Payment Allocation Integrity Rules

> **Task:** V0-DOM-004
> **Status:** InProgress
> **Assignee:** codex-v0-dom-004
> **Work type:** decision
> **Source basis:** PDF:I.11-I.15, PDF:II.2.6, PDF:II.3.4-II.3.5, PDF:II.5.3, PDF:III.8, CORR:C4
> **Date:** 2026-07-30

## 1. Decision Record

| Field | Value |
|-------|-------|
| **Decision ID** | V0-DOM-004-D001 |
| **Date** | 2026-07-30 |
| **Approver** | TBD |
| **Selected result** | PaymentAllocation as immutable ledger with cross-entity invariant enforcement |
| **Rejected alternatives** | Mutable bill.paid_amount (loss of audit trail); Single payment per bill (inflexible for split) |

## 2. PaymentAllocation Model

### Core Schema

```sql
CREATE TABLE payment_allocations (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    payment_id      UUID NOT NULL REFERENCES payments(id) ON DELETE RESTRICT,
    bill_id         UUID NOT NULL REFERENCES bills(id) ON DELETE RESTRICT,
    amount          NUMERIC(12,2) NOT NULL CHECK (amount > 0),
    currency        VARCHAR(3) NOT NULL DEFAULT 'TRY' CHECK (currency IN ('TRY', 'USD', 'EUR')),
    allocated_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    idempotency_key VARCHAR(100) UNIQUE,
    compensating_for UUID REFERENCES payment_allocations(id),
    CONSTRAINT chk_same_currency CHECK (currency = (SELECT currency FROM bills WHERE id = bill_id))
);
```

### Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Immutable allocation | Append-only | Full audit trail, no data loss |
| Idempotency key | Unique | Prevents duplicate allocation on retry |
| Compensating record | Self-referencing FK | Enables reversal without mutation |
| Currency check | CHECK constraint | Prevents cross-currency allocation at DB level |
| `ON DELETE RESTRICT` | Restrict | Prevents orphan allocations |

## 3. Integrity Rules

### Rule 1: Same-Bill Check
A PaymentAllocation MUST reference the same Bill as its parent Payment. Cross-bill allocation is forbidden.

### Rule 2: Currency Equality
The allocation currency MUST match the Bill currency. Cross-currency allocation is rejected.

### Rule 3: Remaining Amount
The sum of all PaymentAllocations for a Bill MUST NOT exceed the Bill total.
```
SUM(payment_allocations.amount) WHERE bill_id = X <= bills.total WHERE id = X
```

### Rule 4: Validity Window
A PaymentAllocation can only be created when the Payment is in `Captured` state and the Bill is in `Open` or `PartiallyPaid` state.

### Rule 5: Idempotency
Each allocation MUST carry a unique idempotency key. Retrying the same allocation with the same key returns the existing allocation without side effects.

### Rule 6: Compensating Records
An incorrect allocation can be reversed by creating a compensating PaymentAllocation with negative amount, linked via `compensating_for`. The original allocation is NOT deleted or modified.

## 4. Invariants

1. **No over-allocation**: `SUM(allocated) <= bill.total` per Bill.
2. **No cross-bill**: Payment and allocation MUST reference the same Bill.
3. **No cross-currency**: Allocation currency MUST match Bill currency.
4. **Immutable**: Once created, a PaymentAllocation cannot be modified. Only compensated.
5. **Idempotent**: Same idempotency key always returns same result.
6. **State-gated**: Allocation only allowed when Payment=Captured AND Bill in (Open, PartiallyPaid).

## 5. Positive Examples

### Example 1: Single payment, full allocation
- Bill A: total 100 TL
- Payment A: captured 100 TL
- Allocation: payment=A, bill=A, amount=100
- Result: Bill A → Settled

### Example 2: Split payment
- Bill A: total 100 TL
- Payment A: captured 60 TL → Allocation: 60
- Payment B: captured 40 TL → Allocation: 40
- Result: Bill A → Settled (60+40=100)

## 6. Negative Examples

### Example 1: Over-allocation
- Bill A: total 100 TL
- Payment A: captured 100 TL → Allocation: 100
- Payment B: captured 50 TL → Allocation: 50 (attempted)
- Result: Rejected — cumulative (150) > bill total (100)

### Example 2: Cross-bill allocation
- Payment A references Bill A
- Allocation attempts to reference Bill B
- Result: Rejected — same-bill check fails

### Example 3: Duplicate replay (no idempotency)
- Payment A: captured 100 TL → Allocation: 100 (idempotency_key=X)
- Retry with same key X → Returns existing allocation, no duplicate
- Retry with different key Y → Rejected — over-allocation

## 7. Consumer Task Interface

### Input
```json
{
  "paymentId": "uuid",
  "billId": "uuid",
  "amount": 60.00,
  "currency": "TRY",
  "idempotencyKey": "unique-key-123"
}
```

### Output
```json
{
  "allocationId": "uuid",
  "billRemaining": 40.00,
  "billNewState": "PartiallyPaid"
}
```

### Error Output
```json
{
  "success": false,
  "error": "OVER_ALLOCATION | CROSS_BILL | CROSS_CURRENCY | INVALID_PAYMENT_STATE | INVALID_BILL_STATE",
  "details": "string"
}
```

## 8. Affected Tasks

- V12-ALC-001 (Payment allocation implementation)
- V12-ALC-002 (Payment allocation edge cases)
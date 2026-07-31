# Refund Ledger Contract

> **Task:** V0-DOM-003
> **Status:** Done
> **Assignee:** codex-v0-dom-003
> **Work type:** decision
> **Source basis:** PDF:II.2.6, PDF:II.3.4-II.3.5, PDF:II.5.3, PDF:III.8
> **Date:** 2026-07-30

## 1. Decision Record

| Field | Value |
|-------|-------|
| **Decision ID** | V0-DOM-003-D001 |
| **Date** | 2026-07-30 |
| **Approver** | TBD |
| **Selected result** | Immutable refund ledger with reversal references |
| **Rejected alternatives** | Mutable payment amount updates (loss of audit trail); Soft-delete with flags (complexity, no clear benefit) |

## 2. Refund Ledger Model

### Core Schema

```sql
CREATE TABLE refund_ledger_entries (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    payment_id          UUID NOT NULL REFERENCES payments(id) ON DELETE RESTRICT,
    bill_id             UUID NOT NULL REFERENCES bills(id) ON DELETE RESTRICT,
    fiscal_document_id  UUID REFERENCES fiscal_documents(id) ON DELETE RESTRICT,
    amount              NUMERIC(12,2) NOT NULL CHECK (amount > 0),
    refund_type         VARCHAR(20) NOT NULL CHECK (refund_type IN ('full', 'partial')),
    reason              VARCHAR(500) NOT NULL,
    reversed_by         UUID REFERENCES refund_ledger_entries(id),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by          UUID NOT NULL, -- user/actor reference
    CONSTRAINT chk_no_double_full CHECK (refund_type = 'full' AND amount = (SELECT COALESCE(SUM(amount), 0) FROM payments WHERE id = payment_id))
);
```

### Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Immutable ledger | Append-only | Full audit trail, no data loss |
| Reversal reference | `reversed_by` FK | Links reversal to original refund, enables chain traversal |
| Fiscal document link | Nullable FK | Refund may or may not have fiscal reversal document |
| `refund_type` enum | full / partial | Clear semantics for downstream consumers |
| `ON DELETE RESTRICT` | Restrict | Prevents orphan refund entries |

## 3. Refund Rules

### Rule 1: Cumulative Refund Limit
Total refunded amount for a Payment MUST NOT exceed the original captured amount.
```
SUM(refund_ledger_entries.amount) WHERE payment_id = X <= payments.captured_amount WHERE id = X
```

### Rule 2: Partial Refund Allocation
A partial refund MUST specify which PaymentAllocation line items are being refunded. If not specified at item level, the refund is distributed proportionally across all items in the Bill.

### Rule 3: Bill Reopening
A refund on a settled Bill MUST reopen the Bill to `PartiallyPaid` or `Open` state if the refund reduces the paid amount below the Bill total.

### Rule 4: Fiscal Linkage
If a refund requires a fiscal cancellation or credit note, the `fiscal_document_id` MUST reference the issued fiscal document. Refunds without fiscal linkage are permitted only for non-fiscal payments (e.g., meal card).

### Rule 5: Double Refund Prevention
A refund entry with `reversed_by` set is considered reversed. A reversed refund cannot be reversed again. A payment cannot have two active (non-reversed) full refunds.

## 4. Invariants

1. **No over-refund**: `SUM(refunded) <= SUM(captured)` per payment.
2. **No double refund**: A payment can have at most one active full refund.
3. **Immutable**: Once written, a refund ledger entry cannot be modified. Only reversed via `reversed_by`.
4. **Audit trail**: Every refund entry MUST record actor, timestamp, reason, and fiscal document reference.
5. **Bill consistency**: After refund, Bill state MUST reflect the new paid amount.

## 5. Positive Examples

### Example 1: Full Refund
- Payment A: captured 100 TL
- Refund entry: amount=100, type=full, reason="Customer returned order"
- Result: Payment A net = 0, Bill reopened to Open

### Example 2: Partial Refund
- Payment A: captured 100 TL (items: 1=60, 2=40)
- Refund entry: amount=40, type=partial, reason="Item 2 returned", references item 2
- Result: Payment A net = 60, Bill remains Settled (60 >= 60 minimum)

## 6. Negative Examples

### Example 1: Over-refund
- Payment A: captured 100 TL
- Refund 1: amount=80
- Refund 2: amount=30 (attempted)
- Result: Refund 2 rejected — cumulative (110) > captured (100)

### Example 2: Double full refund
- Payment A: captured 100 TL
- Refund 1: amount=100, type=full
- Refund 2: amount=100, type=full (attempted)
- Result: Refund 2 rejected — payment already has active full refund

## 7. Consumer Task Interface

### Input
```json
{
  "paymentId": "uuid",
  "billId": "uuid",
  "amount": 40.00,
  "refundType": "partial",
  "reason": "Item returned",
  "fiscalDocumentId": "uuid | null",
  "createdBy": "uuid"
}
```

### Output
```json
{
  "refundEntryId": "uuid",
  "paymentNetAmount": 60.00,
  "billNewState": "Settled"
}
```

### Error Output
```json
{
  "success": false,
  "error": "OVER_REFUND | DOUBLE_FULL_REFUND | PAYMENT_NOT_FOUND | FISCAL_DOCUMENT_REQUIRED",
  "details": "string"
}
```

## 8. Affected Tasks

- V12-ALC-003 (Payment allocation)
- V12-HUG-003 (Hugin refund integration)
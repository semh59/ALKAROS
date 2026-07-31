# Table Reservation Policy

> **Task:** V0-DOM-005
> **Status:** Done
> **Assignee:** codex-v0-dom-005
> **Work type:** decision
> **Source basis:** PDF:II.2.3, PDF:II.3.16, PDF:II.5.15, PDF:III.5, CORR:C5
> **Date:** 2026-07-30

## 1. Decision Record

| Field | Value |
|-------|-------|
| **Decision ID** | V0-DOM-005-D001 |
| **Date** | 2026-07-30 |
| **Approver** | TBD |
| **Selected result** | Reservation as first-class entity with owner, reason, expiry, and cancellation |
| **Rejected alternatives** | Table-level flag only (no audit trail, no expiry); Soft-delete reservation (complexity) |

## 2. Reservation Model

### Core Schema

```sql
CREATE TABLE reservations (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    table_id        UUID NOT NULL REFERENCES tables(id) ON DELETE RESTRICT,
    actor           VARCHAR(50) NOT NULL CHECK (actor IN ('host', 'waiter', 'customer_qr', 'system')),
    actor_id        UUID, -- user or customer reference
    reason          VARCHAR(500) NOT NULL,
    reserved_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at      TIMESTAMPTZ NOT NULL,
    cancelled_at    TIMESTAMPTZ,
    cancelled_by    UUID,
    cancel_reason   VARCHAR(500),
    CONSTRAINT chk_expiry_after_reserve CHECK (expires_at > reserved_at)
);
```

### Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Reservation as entity | Separate table | Full audit trail, expiry, cancellation tracking |
| `actor` enum | host/waiter/customer_qr/system | Clear ownership of reservation |
| `expires_at` | Required | Prevents abandoned reservations blocking tables |
| `cancelled_at` | Nullable | Soft cancellation with audit trail |
| QR limitation | QR cannot create reservation | QR is for menu/view only; reservation requires explicit actor |

## 3. Reservation Rules

### Rule 1: Reservation Ownership
Every reservation MUST have an actor and reason. A reservation without an owner is invalid.

### Rule 2: Expiry
A reservation in `Reserved` state automatically transitions to `Available` when `expires_at` is reached. The system MUST check expiry on any Table state query.

### Rule 3: Cancellation
A reservation can be cancelled by:
- The original actor (host/waiter)
- A manager
- System (e.g., table reassignment)

Cancellation MUST record `cancelled_at`, `cancelled_by`, and `cancel_reason`.

### Rule 4: QR Cannot Reserve
The QR customer flow (menu view, order) MUST NOT create a reservation. QR is read-only for table state. Only `host`, `waiter`, or `system` actors can create reservations.

### Rule 5: Occupancy Priority
When a Table is `Reserved` and a walk-in customer arrives:
- If the reservation is within 15 minutes of `expires_at`, the host may offer the table to walk-in
- If the reservation has expired, the table is automatically `Available`
- A walk-in cannot override an active (non-expired) reservation without manager approval

### Rule 6: Concurrent Reservation Prevention
A Table can have at most one active (non-expired, non-cancelled) reservation at any time.

## 4. Invariants

1. **Single active reservation**: A Table may have at most one reservation where `expires_at > now()` AND `cancelled_at IS NULL`.
2. **QR cannot reserve**: The `customer_qr` actor MUST NOT create reservations.
3. **Expiry enforcement**: `expires_at` MUST be in the future at creation time.
4. **Audit trail**: Every reservation creation, cancellation, and expiry MUST be logged.
5. **Table state consistency**: When a reservation expires or is cancelled, Table state MUST return to `Available`.

## 5. Positive Examples

### Example 1: Host reserves table
- Host creates reservation for Table 5, 19:00-21:00
- Table 5: Available → Reserved
- At 21:00, reservation expires → Table 5: Reserved → Available

### Example 2: Waiter cancels reservation
- Waiter creates reservation for Table 3
- Customer cancels; waiter cancels reservation with reason "customer cancelled"
- Table 3: Reserved → Available immediately

## 6. Negative Examples

### Example 1: QR attempts reservation
- Customer scans QR code on Table 7
- QR flow attempts to create reservation with actor=customer_qr
- Result: Rejected — QR cannot create reservations

### Example 2: Double reservation
- Table 2 has active reservation (19:00-21:00)
- Host attempts to create another reservation for Table 2 at 19:30
- Result: Rejected — table already has active reservation

## 7. Consumer Task Interface

### Input (Create)
```json
{
  "tableId": "uuid",
  "actor": "host | waiter | system",
  "actorId": "uuid",
  "reason": "Customer requested",
  "expiresAt": "2026-07-30T21:00:00Z"
}
```

### Output
```json
{
  "reservationId": "uuid",
  "tableId": "uuid",
  "newState": "Reserved",
  "expiresAt": "2026-07-30T21:00:00Z"
}
```

### Error Output
```json
{
  "success": false,
  "error": "TABLE_ALREADY_RESERVED | QR_CANNOT_RESERVE | INVALID_ACTOR | EXPIRY_IN_PAST",
  "details": "string"
}
```

## 8. Affected Tasks

- V1-TBL-004 (Table management)
- V14-QRO-002 (QR order flow)
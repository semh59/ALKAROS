# Void, Complimentary and Discount Policy

> **Task:** V0-DOM-006
> **Status:** Blocked
> **Assignee:** codex-v0-dom-006
> **Work type:** decision
> **Source basis:** PDF:II.2.5, PDF:II.3.3, PDF:II.5.2, PDF:III.7
> **Date:** 2026-07-30

## 1. Operation Types

| Operation | Description | Tax Impact | Actor | Approval Threshold |
| ----------- | ------------- | ------------ | ------- | ------------------- |
| Void | Item removed before payment | Reverses tax | Waiter, Manager | >100 TL requires manager |
| Complimentary | Item given free | Tax still applies | Manager only | All amounts |
| Discount | Price reduction | Proportional tax reduction | Waiter (up to %10), Manager (any) | >%10 requires manager |
| Waste | Item discarded (inventory) | N/A (no sale) | Kitchen, Manager | >50 TL requires manager |

## 2. Eligibility Rules

### Void

- Item MUST be in `Queued` or `Cooking` state (KitchenTicketItem).
- If item is already `Done` or `Served`, use Refund instead.
- Voided item's inventory reservation is released.

### Complimentary

- Item MUST be on an active Order.
- Complimentary items still appear on the Bill with price=0 and tax calculated.
- Manager approval required for all complimentary items.

### Discount

- Applied at line level or bill level.
- Line-level discount: specific item price reduced.
- Bill-level discount: distributed proportionally (per CMP-002 rules).
- Total discount MUST NOT exceed bill total.

### Waste

- Only for production/kitchen items, not for customer orders.
- Waste records decrement inventory without a sale.

## 3. Invariants

1. **Mutual exclusion**: An item cannot be both voided and discounted.
2. **Audit trail**: Every operation MUST record actor, reason, amount, and timestamp.
3. **Tax consistency**: Complimentary items still incur tax liability.
4. **Approval enforcement**: Operations above threshold without approval are rejected.

## 4. Affected Tasks

- V1-ORD-003 (Void and complimentary)
- V1-BIL-003 (Discount and fee lines)

# Lifecycle Transition Contracts

> **Task:** V0-DOM-001
> **Status:** Done
> **Assignee:** codex-v0-dom-001
> **Work type:** decision
> **Source basis:** PDF:I.0-I.5, PDF:II.2.4, PDF:II.3.2, PDF:II.5.1, PDF:III.6
> **Corrections (2026-08-01, CORR:C29):** Provider timeout örtük decline/success sayılmaz; terminal sonucu bir kez uygulanır. Payment durumlarına `Unknown`, `ReconciliationRequired`; FiscalDocument durumlarına `Requested`, `Pending`, `Rejected`, `Refunded`, `ReconciliationRequired` eklendi (kaynak: PDF:II.3.15, PDF:II.5.3, PDF:II.5.4).

## 1. Entities and States

### Order
- States: `Draft`, `Active`, `Closed`, `Cancelled`
- Transitions: Draft→Active, Active→Closed, Active→Cancelled, Draft→Cancelled

### Bill
- States: `Open`, `Settled`, `Voided`, `PartiallyPaid`
- Transitions: Open→PartiallyPaid, Open→Settled, PartiallyPaid→Settled, Open→Voided
- `Settled` is terminal: a refund MUST NOT reopen a settled Bill; refunds net via the refund ledger and compensating allocations (V0-DOM-003, V12-ALC-004, docs/domain/refund-ledger.md Rule 3).

### Payment
- States: `Pending`, `Authorized`, `Captured`, `Failed`, `Refunded`, `PartiallyRefunded`, `Unknown`, `ReconciliationRequired`
- Transitions: Pending→Authorized, Pending→Failed, Pending→Unknown, Unknown→Authorized, Unknown→Captured, Unknown→Failed, Unknown→ReconciliationRequired, Authorized→Captured, Authorized→Failed, Authorized→Unknown, Captured→Refunded, Captured→PartiallyRefunded, PartiallyRefunded→Refunded

### FiscalDocument
- States: `Requested`, `Pending`, `Issued`, `Rejected`, `Cancelled`, `Refunded`, `ReconciliationRequired`
- Transitions: Requested→Pending, Pending→Issued, Pending→Rejected, Pending→ReconciliationRequired, Issued→Cancelled, Issued→Refunded

### ProductionBatch
- States: `Planned`, `InProgress`, `Completed`, `Cancelled`
- Transitions: Planned→InProgress, InProgress→Completed, Planned→Cancelled, InProgress→Cancelled

### PortionReservation
- States: `Reserved`, `Consumed`, `Released`, `Expired`
- Transitions: Reserved→Consumed, Reserved→Released, Reserved→Expired

### KitchenTicket
- States: `Pending`, `Preparing`, `Ready`, `Served`, `Cancelled`
- Transitions: Pending→Preparing, Preparing→Ready, Ready→Served, Pending→Cancelled, Preparing→Cancelled

### KitchenTicketItem
- States: `Queued`, `Cooking`, `Done`, `Cancelled`
- Transitions: Queued→Cooking, Cooking→Done, Queued→Cancelled, Cooking→Cancelled

### PrintJob
- States: `Queued`, `Printing`, `Completed`, `Failed`, `Cancelled`
- Transitions: Queued→Printing, Printing→Completed, Printing→Failed, Queued→Cancelled, Failed→Queued (retry)

### CashSession
- States: `Open`, `Closed`, `Reconciled`
- Transitions: Open→Closed, Closed→Reconciled

### MealCardSettlement
- States: `Pending`, `Submitted`, `Settled`, `Failed`
- Transitions: Pending→Submitted, Submitted→Settled, Submitted→Failed, Failed→Pending (retry)

### Invoice
- States: `Draft`, `Issued`, `Paid`, `Cancelled`, `CreditNote`
- Transitions: Draft→Issued, Issued→Paid, Issued→Cancelled, Issued→CreditNote

### ReconciliationCase
- States: `Open`, `Investigating`, `Resolved`, `Escalated`
- Transitions: Open→Investigating, Investigating→Resolved, Investigating→Escalated, Escalated→Resolved

### Alert
- States: `Active`, `Acknowledged`, `Resolved`
- Transitions: Active→Acknowledged, Acknowledged→Resolved, Active→Resolved

### Table
- States: `Available`, `Occupied`, `Reserved`, `Cleaning`, `OutOfService`
- Transitions: Available→Occupied, Available→Reserved, Reserved→Occupied, Reserved→Available (expiry/cancellation), Occupied→Cleaning, Cleaning→Available, Available→OutOfService, OutOfService→Available

## 2. Transition Matrix

| Source Entity | Source State | Target State | Actor | Reason | Transaction Boundary | Audit Required | Retry/Failure |
|---|---|---|---|---|---|---|---|
| Order | Draft | Active | Waiter/Cashier | Order submitted | Yes | Yes | N/A |
| Order | Active | Closed | Cashier | Bill settled | Yes | Yes | N/A |
| Order | Active | Cancelled | Manager | Order voided | Yes | Yes | N/A |
| Order | Draft | Cancelled | Waiter/Cashier | Order discarded | Yes | Yes | N/A |
| Bill | Open | PartiallyPaid | Payment | Partial payment received | Yes | Yes | N/A |
| Bill | Open | Settled | Payment | Full payment received | Yes | Yes | N/A |
| Bill | PartiallyPaid | Settled | Payment | Remaining payment received | Yes | Yes | N/A |
| Bill | Open | Voided | Manager | Bill voided | Yes | Yes | N/A |
| Payment | Pending | Authorized | PaymentProvider | Authorization success | Yes | Yes | N/A |
| Payment | Pending | Failed | PaymentProvider | Authorization declined | Yes | Yes | N/A |
| Payment | Pending | Unknown | PaymentProvider | Authorization timeout/connection loss | Yes | Yes | No implicit decline; query terminal, apply result once |
| Payment | Unknown | Authorized | System | Terminal query confirms authorization | Yes | Yes | N/A |
| Payment | Unknown | Captured | System | Terminal query confirms capture | Yes | Yes | N/A |
| Payment | Unknown | Failed | System | Terminal query confirms decline | Yes | Yes | N/A |
| Payment | Unknown | ReconciliationRequired | System | Unresolvable divergence | Yes | Yes | Create ReconciliationCase |
| Payment | Authorized | Captured | Cashier | Capture confirmed | Yes | Yes | N/A |
| Payment | Authorized | Failed | PaymentProvider | Capture declined | Yes | Yes | N/A |
| Payment | Authorized | Unknown | PaymentProvider | Capture timeout/connection loss | Yes | Yes | No implicit decline; query terminal, apply result once |
| Payment | Captured | Refunded | Manager | Full refund | Yes | Yes | N/A |
| Payment | Captured | PartiallyRefunded | Manager | Partial refund | Yes | Yes | N/A |
| Payment | PartiallyRefunded | Refunded | Manager | Remaining refund | Yes | Yes | N/A |
| FiscalDocument | Requested | Pending | FiscalDevice | Fiscalization submitted | Yes | Yes | N/A |
| FiscalDocument | Pending | Issued | FiscalDevice | Fiscalization success | Yes | Yes | N/A |
| FiscalDocument | Pending | Rejected | FiscalDevice | Fiscalization declined by device | Yes | Yes | N/A |
| FiscalDocument | Pending | ReconciliationRequired | System | Timeout/connection loss, unresolvable | Yes | Yes | No implicit issued; query device, apply result once; create ReconciliationCase |
| FiscalDocument | Issued | Cancelled | Manager | Fiscal cancellation | Yes | Yes | N/A |
| FiscalDocument | Issued | Refunded | Manager | Fiscal refund document | Yes | Yes | N/A |
| ProductionBatch | Planned | InProgress | Kitchen | Production started | Yes | Yes | N/A |
| ProductionBatch | InProgress | Completed | Kitchen | Production finished | Yes | Yes | N/A |
| ProductionBatch | Planned | Cancelled | Manager | Batch cancelled | Yes | Yes | N/A |
| ProductionBatch | InProgress | Cancelled | Manager | Batch cancelled mid-production | Yes | Yes | N/A |
| PortionReservation | Reserved | Consumed | Kitchen | Portion used | Yes | Yes | N/A |
| PortionReservation | Reserved | Released | Kitchen | Reservation released | Yes | Yes | N/A |
| PortionReservation | Reserved | Expired | System | Timeout | Yes | Yes | N/A |
| KitchenTicket | Pending | Preparing | Kitchen | Preparation started | Yes | Yes | N/A |
| KitchenTicket | Preparing | Ready | Kitchen | Preparation finished | Yes | Yes | N/A |
| KitchenTicket | Ready | Served | Waiter | Item served to table | Yes | Yes | N/A |
| KitchenTicket | Pending | Cancelled | Waiter/Manager | Ticket cancelled | Yes | Yes | N/A |
| KitchenTicket | Preparing | Cancelled | Manager | Ticket cancelled mid-prep | Yes | Yes | N/A |
| KitchenTicketItem | Queued | Cooking | Kitchen | Cooking started | Yes | Yes | N/A |
| KitchenTicketItem | Cooking | Done | Kitchen | Cooking finished | Yes | Yes | N/A |
| KitchenTicketItem | Queued | Cancelled | Waiter/Manager | Item cancelled | Yes | Yes | N/A |
| KitchenTicketItem | Cooking | Cancelled | Manager | Item cancelled mid-cook | Yes | Yes | N/A |
| PrintJob | Queued | Printing | Printer | Print started | Yes | Yes | Retry 3x, then Failed |
| PrintJob | Printing | Completed | Printer | Print success | Yes | Yes | N/A |
| PrintJob | Printing | Failed | Printer | Print error | Yes | Yes | Retry 3x, then manual |
| PrintJob | Queued | Cancelled | System/User | Print cancelled | Yes | Yes | N/A |
| PrintJob | Failed | Queued | System | Retry | Yes | Yes | Max 3 retries |
| CashSession | Open | Closed | Cashier | Session ended | Yes | Yes | N/A |
| CashSession | Closed | Reconciled | Manager | Reconciliation done | Yes | Yes | N/A |
| MealCardSettlement | Pending | Submitted | System | Batch submitted | Yes | Yes | Retry 3x, then Failed |
| MealCardSettlement | Submitted | Settled | Provider | Settlement confirmed | Yes | Yes | N/A |
| MealCardSettlement | Submitted | Failed | Provider | Settlement rejected | Yes | Yes | N/A |
| MealCardSettlement | Failed | Pending | System | Retry | Yes | Yes | Max 3 retries |
| Invoice | Draft | Issued | System | Invoice generated | Yes | Yes | N/A |
| Invoice | Issued | Paid | Payment | Payment received | Yes | Yes | N/A |
| Invoice | Issued | Cancelled | Manager | Invoice cancelled | Yes | Yes | N/A |
| Invoice | Issued | CreditNote | Manager | Credit note issued | Yes | Yes | N/A |
| ReconciliationCase | Open | Investigating | System/User | Investigation started | Yes | Yes | N/A |
| ReconciliationCase | Investigating | Resolved | Manager | Case resolved | Yes | Yes | N/A |
| ReconciliationCase | Investigating | Escalated | Manager | Case escalated | Yes | Yes | N/A |
| ReconciliationCase | Escalated | Resolved | Manager | Case resolved after escalation | Yes | Yes | N/A |
| Alert | Active | Acknowledged | User | Alert seen | Yes | Yes | N/A |
| Alert | Acknowledged | Resolved | User | Issue resolved | Yes | Yes | N/A |
| Alert | Active | Resolved | System | Auto-resolved | Yes | Yes | N/A |
| Table | Available | Occupied | Host | Table assigned | Yes | Yes | N/A |
| Table | Available | Reserved | Host | Table reserved | Yes | Yes | N/A |
| Table | Reserved | Occupied | Host | Reservation seated | Yes | Yes | N/A |
| Table | Reserved | Available | System/Operator | Reservation expired or cancelled | Yes | Yes | N/A |
| Table | Occupied | Cleaning | Staff | Table being cleaned | Yes | Yes | N/A |
| Table | Cleaning | Available | Staff | Table ready | Yes | Yes | N/A |
| Table | Available | OutOfService | Manager | Table out of service | Yes | Yes | N/A |
| Table | OutOfService | Available | Manager | Table back in service | Yes | Yes | N/A |

## 3. Cross-Entity Transition Rules

### Order → Bill
- An Order in `Active` state MUST have at least one Bill in `Open` or `PartiallyPaid` state (an Order may be split across multiple Bills via `bill_order_items` — V0-DOM-002).
- Bill transitions to `Settled` or `Voided` trigger Order to `Closed` or `Cancelled` respectively.

### Bill → Payment
- A Bill in `Open` or `PartiallyPaid` state MUST have at least one associated Payment.
- Total captured Payment amount MUST equal Bill total for `Settled` transition.

### Bill → Refund
- A refund on a `Settled` Bill MUST NOT transition the Bill out of `Settled`; the refund nets through the refund ledger
  and compensating allocations (V0-DOM-003, V12-ALC-004, docs/domain/refund-ledger.md Rule 3).

### Payment → FiscalDocument
- A captured Payment MUST have an associated FiscalDocument in `Issued` state.
- FiscalDocument MUST be issued before or at the same time as Payment capture.

### Payment → ReconciliationCase
- A Payment that reaches `Unknown` and cannot be resolved by terminal query MUST create a ReconciliationCase in `Open` state.
- A FiscalDocument that reaches `ReconciliationRequired` MUST have an associated ReconciliationCase.

### Order → KitchenTicket
- An Order in `Active` state with food items MUST have at least one KitchenTicket.
- KitchenTicket state changes are independent of Order state (kitchen can continue preparing after Order is closed).

### KitchenTicket → KitchenTicketItem
- A KitchenTicket MUST contain at least one KitchenTicketItem.
- KitchenTicketItem states are tracked independently within the parent KitchenTicket.

### ProductionBatch → PortionReservation
- A completed ProductionBatch creates PortionReservations for inventory tracking.
- PortionReservation `Consumed` transition decrements inventory.

### Payment → MealCardSettlement
- Meal card Payments MUST be batched into MealCardSettlement for provider submission.
- Settlement state is independent of individual Payment state.

### CashSession → ReconciliationCase
- A CashSession `Closed`→`Reconciled` transition that detects a variance MUST create a ReconciliationCase.

### Alert → Any Entity
- Alerts can reference any entity and are resolved independently of the referenced entity's state.

## 4. Invariants

1. **No orphan transitions**: Every transition must have a valid source and target state as defined in the matrix above.
2. **No wildcard transitions**: All transitions are explicitly listed. Any transition not in the matrix is forbidden.
3. **Atomicity**: Each transition is atomic within its transaction boundary. Partial transitions are not permitted.
4. **Audit trail**: Every transition MUST produce an audit log entry with timestamp, actor, source state, target state, and reason.
5. **Retry limit**: All retry-capable transitions have a maximum of 3 retry attempts before manual intervention is required.
6. **No implicit timeout outcome**: A provider timeout MUST NOT be treated as an implicit success or decline; the terminal/device MUST be queried and the confirmed result applied exactly once. Unresolvable outcomes transition to `ReconciliationRequired` and create a ReconciliationCase.
7. **Cascade rules**: Cross-entity transitions follow the rules in Section 3. Violations MUST be detected and rejected.

## 5. Positive Examples

### Example 1: Successful payment flow
1. Order: Draft → Active (Waiter submits order)
2. Bill: Open → PartiallyPaid (Customer pays partially)
3. Payment: Pending → Authorized → Captured
4. FiscalDocument: Requested → Pending → Issued
5. Bill: PartiallyPaid → Settled
6. Order: Active → Closed

### Example 2: Table turnover
1. Table: Available → Occupied (Host seats guests)
2. Order: Draft → Active
3. Bill: Open → Settled
4. Order: Active → Closed
5. Table: Occupied → Cleaning → Available

## 6. Negative Examples

### Example 1: Forbidden direct transition
- Bill: Open → Closed (INVALID — Bill must go through Settled or Voided)
- Result: Transition rejected, error returned

### Example 2: Payment capture without fiscal document
- Payment: Authorized → Captured without FiscalDocument: Requested → Pending → Issued
- Result: Transition blocked, FiscalDocument must be issued first

## 7. Consumer Task Interface

### Input
```json
{
  "entityType": "Order | Bill | Payment | ...",
  "entityId": "uuid",
  "transition": "Draft→Active",
  "actor": "Waiter | Cashier | Manager | System | ...",
  "reason": "string",
  "timestamp": "ISO8601"
}
```

### Output
```json
{
  "success": true,
  "newState": "Active",
  "auditId": "uuid"
}
```

### Error Output
```json
{
  "success": false,
  "error": "INVALID_TRANSITION | TRANSACTION_FAILED | INVARIANT_VIOLATION",
  "details": "string"
}
```

### Invariants for Consumers
1. All transitions MUST go through the transition validation function.
2. No entity may change state without an audit log entry.
3. Cross-entity invariants MUST be checked before any transition is committed.
4. Retry logic MUST respect the max retry limit (3).
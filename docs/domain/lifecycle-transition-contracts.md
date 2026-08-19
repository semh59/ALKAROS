# Lifecycle Transition Contracts — approved decision record

> **Task:** V0-DOM-001
> **Status:** Done
> **Assignee:** codex-v0-dom-001
> **Work type:** decision
> **Source basis:** PDF:I.46, PDF:I.46A, PDF:II.5.1-II.5.15, PDF:III.6-III.23,
> PDF:I.8, PDF:I.13, PDF:I.16, PDF:I.27.1, PDF:I.28, PDF:I.28.1, CORR:C29
> **Access date:** PDF source 2026-07-29; artifact verification 2026-08-02
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (PDF baseline + CORR:C29 + named approver)

## Canonical state sets (PDF:I.46A / PDF:II.5 — copied unchanged)

- Order: `Draft, Submitted, PendingConfirmation, Accepted, Rejected, Preparing, Ready, Served, Completed, Cancelled`
- Bill: `Open, PartiallyAllocated, Allocated, PartiallyPaid, Paid, Cancelled, Reopened`
- Payment: `Initiated, Pending, Approved, Declined, Cancelled, Unknown, ReconciliationRequired, Refunded`
  (+ `PartiallyRefunded`, approved 2026-08-03, see V0-DOM-003)
- FiscalDocument: `Requested, Pending, Issued, Rejected, Cancelled, Refunded, ReconciliationRequired`
- ProductionBatch: `Planned, InProgress, Completed, Cancelled`
- PortionReservation: `Reserved, Released, Consumed, Waste` (`Available` is a stock-pool condition, not a state)
- KitchenTicket: `Queued, Accepted, Preparing, Ready, Cancelled`
- KitchenTicketItem: `Queued, Preparing, Ready, Served, Cancelled`
- PrintJob: `Pending, Printing, Printed, Failed, Retrying, Cancelled`
- CashSession: `Open, Counting, Closing, Closed, Reconciled`
- MealCardSettlement: `Open, Prepared, Submitted, PartiallySettled, Settled, Disputed, Reconciled`
- MealCardPayment.settlement_status: `Unsettled, IncludedInSettlement, Settled, Disputed`
- Invoice: `Draft, Validating, PendingProvider, Issued, Rejected, Cancelled, ReconciliationRequired`
- ReconciliationCase: `Open, Investigating, WaitingProvider, Resolved, Dismissed, Escalated`
- Alert: `Open, Acknowledged, Escalated, Suppressed, Resolved`
- Table: `Available, Occupied, Reserved, Cleaning, OutOfService` (application-layer
  invariant only, not a DB constraint)
- InventoryMovement: no state machine — immutable ledger entry;
  `MovementType: PurchaseReceipt, ProductionOutput, Consumption, Reservation, Release, Waste, Adjustment, Return,
Reversal`

PDF:II.5: "Database status values MUST match them exactly." No state, rename or
deletion outside this list.

## General transition contract (PDF:II.5)

For every machine: allowed transitions are explicit; forbidden transitions are
rejected; actor and transition reason are recorded where required; transition
and side effects share a transaction boundary where local consistency requires
it; idempotency is mandatory for external callbacks/retries; failures create
recoverable state or a ReconciliationCase; terminal states cannot silently
reopen.

## Timeout rule (CORR:C29, PDF:I.27.1)

Payment/FiscalDocument timeout is neither implicit approval nor implicit
decline. Flow: `Pending → Unknown → ReconciliationRequired` (with
ReconciliationCase) → `Approved / Declined / Cancelled`.

## Approved decisions (2026-08-03)

1. **Retry policy (provider timeout):** 3 attempts with 2s / 5s / 15s backoff;
   after the third failure the flow enters `Unknown` (Payment) /
   `ReconciliationRequired` (FiscalDocument) and a ReconciliationCase is
   opened. No implicit terminal outcome (CORR:C29).
2. **Fiscal trigger:** a Payment reaching financially final `Approved` state
   requests the FiscalDocument (`Requested`) per PDF:I.28
   ("Payment → Fiscal strategy → FiscalDocument"); payment cancellation or
   refund triggers the fiscal refund/cancel pathway (PDF:I.28.1) per the
   verified Hugin contract. Provider-specific ordering beyond this direction
   is delegated to V12-FSC-*/V12-HUG-* tasks.
3. **Reopen policy:** only `Bill.Reopened` exists in the canonical set; the
   Bill transitions `Paid/Cancelled → Reopened` only through an explicit,
   audited domain action. All other terminal states (`Completed`, `Cancelled`,
   `Settled`, `Reconciled`, `Resolved`, `Issued`, `Closed`, `Anonymized`…)
   never reopen; corrections are new domain actions (PDF:II.1.3).

## Transition matrix (testable; no wildcard edges)

Legend: allowed edges are forward canonical paths plus explicit exceptions;
forbidden edges are rejected.

| Machine | Allowed edges | Forbidden edges (examples) |
| --- | --- | --- |
| Order | Draft→Submitted; Submitted→PendingConfirmation; PendingConfirmation→Accepted\|Rejected; Accepted→Preparing; Preparing→Ready; Ready→Served; Served→Completed; {Draft,Submitted,PendingConfirmation,Accepted,Preparing,Ready}→Cancelled | Served→Accepted; Completed→Preparing; Draft→Accepted (no skip); Cancelled→Accepted (terminal reopen) |
| Bill | Open→PartiallyAllocated→Allocated→PartiallyPaid→Paid; any non-terminal→Cancelled; Paid\|Cancelled→Reopened (explicit audited action) | Paid→Open (silent); Cancelled→Allocated; Open→Paid while payable unmet (PDF:I.13.2) |
| Payment | Initiated→Pending; Pending→Approved\|Declined\|Cancelled\|Unknown; Unknown→ReconciliationRequired; ReconciliationRequired→Approved\|Declined\|Cancelled; Approved→Refunded\|PartiallyRefunded (V0-DOM-003) | Approved→Initiated; Declined→Approved; Pending→Refunded (skip); Approved→Approved (no-op transition) |
| FiscalDocument | Requested→Pending; Pending→Issued\|Rejected\|Cancelled\|ReconciliationRequired; Issued→Refunded | Issued→Pending; Cancelled→Issued; timeout→Issued (CORR:C29) |
| ProductionBatch | Planned→InProgress; InProgress→Completed\|Cancelled | Completed→InProgress; Planned→Completed |
| PortionReservation | Reserved→Released\|Consumed\|Waste | Released→Consumed; Waste→Reserved |
| KitchenTicket | Queued→Accepted; Accepted→Preparing; Preparing→Ready; Ready→Cancelled; {Queued,Accepted,Preparing}→Cancelled | Ready→Accepted; Cancelled→Queued (print retry does not reopen ticket) |
| KitchenTicketItem | Queued→Preparing; Preparing→Ready; Ready→Served; {Queued,Preparing,Ready}→Cancelled | Served→Ready; Cancelled→Preparing; parent Ready before all non-cancelled items Ready/Served (PDF:II.5.7A) |
| PrintJob | Pending→Printing; Printing→Printed\|Failed; Failed→Retrying; Retrying→Printing\|Failed\|Cancelled | Printed→Printing; Failed→Printed (no silent success); Cancelled→Retrying |
| CashSession | Open→Counting; Counting→Closing; Closing→Closed; Closed→Reconciled | Reconciled→Open; Closed→Counting |
| MealCardSettlement | Open→Prepared→Submitted; Submitted→PartiallySettled; PartiallySettled→Settled; {Open,Prepared,Submitted,PartiallySettled}→Disputed; Disputed→Settled\|Reconciled; Settled→Reconciled | Settled→Submitted; Reconciled→Open |
| settlement_status | Unsettled→IncludedInSettlement (only via settlement items row in Prepared+); IncludedInSettlement\|Unsettled→Settled (parent Settled); Unsettled\|IncludedInSettlement→Disputed (with ReconciliationCase) | Settled→IncludedInSettlement; Disputed→Settled without case resolution |
| Invoice | Draft→Validating; Validating→PendingProvider; PendingProvider→Issued\|Rejected\|ReconciliationRequired; {Draft,Validating,PendingProvider}→Cancelled | Issued→PendingProvider; Rejected→Issued; incoming provider invoices are not forced into this lifecycle (PDF:II.5.11) |
| ReconciliationCase | Open→Investigating; Investigating→WaitingProvider; {Open,Investigating,WaitingProvider}→Resolved\|Dismissed\|Escalated | Resolved→Investigating; Dismissed→Open |
| Alert | Open→Acknowledged; Open→Escalated; Acknowledged→Escalated; Open\|Acknowledged\|Escalated→Suppressed; {Open,Acknowledged,Escalated,Suppressed}→Resolved | Resolved→Open; Suppressed→Escalated without new event |
| Table | Available→Occupied\|Reserved\|Cleaning\|OutOfService; Occupied→Reserved (via order); {Occupied,Reserved,Cleaning}→Available; Available→OutOfService; OutOfService→Available\|Cleaning | Available→Available (no-op); Table state never drives financial transitions (application-layer only) |

## Examples

Positive:

- Waiter submits order → `Draft→Submitted→PendingConfirmation`; kitchen
  acceptance → `Accepted`; ticket `Queued→Accepted→Preparing→Ready` — all
  recorded with actor and reason.
- Cash tender times out → `Pending→Unknown→ReconciliationRequired` +
  ReconciliationCase → operator verifies → `Approved` (PDF:I.27.1, CORR:C29).

Negative:

- A 3x-retry timeout policy that ends in `Failed`/`Declined` is forbidden
  (CORR:C29): timeout must route through `Unknown`/`ReconciliationRequired`.
- A `Paid` Bill silently reopened to add items is forbidden; only explicit
  `Bill.Reopened` with audit trail (PDF:II.5.2).

## Invariants for consumers

- No transition outside the canonical sets (PDF:II.5).
- No implicit timeout outcome (CORR:C29).
- Table transitions never enforce financial coupling at DB level (PDF:II.5.15).
- `SplitPayment` is neither a Payment state nor a Payment method (PDF:II.5.3).
- External callback transitions require idempotency (PDF:II.5).

## Affected tasks

- Handoff: V0-DAT-002.
- CORR:C29 consumers: V12-HUG-001, V12-HUG-002, V12-PAY-003, V12-PAY-004,
  V12-FSC-001, V12-REC-001.
- Consumers (dependency rows): V0-ARC-001, V0-DOC-001, V0-DOM-004, V0-DOM-005,
  V11-RCP-001, V12-CSH-001, V12-MCD-002, V14-MAP-002, V1-TBL-001, V1-REC-001.

## Acceptance evidence

- Transition matrix above gives every state at least one allowed and one
  forbidden edge; no wildcard transitions.
- Decision record with source, access dates, approver (Semih, 2026-08-03),
  selected result, rejected alternatives and affected task IDs.

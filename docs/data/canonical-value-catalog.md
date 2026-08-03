# Canonical Value Catalog — PDF extraction pending approval

> **Task:** V0-DAT-002
> **Status:** Blocked
> **Assignee:** codex-v0-dat-002
> **Work type:** decision
> **Source basis:** PDF:II.5.1-II.5.15, PDF:III.3-III.40, CORR:C2, CORR:C7
> **Access date:** 2026-08-02
> **Approver:** None — decision is not approved

## Verified state values

PDF Part II.5 states that database status values must match the following
state machines exactly. This section is an extraction, not an approved
extension point.

| Entity | Canonical values |
| --- | --- |
| Order | `Draft`, `Submitted`, `PendingConfirmation`, `Accepted`, `Rejected`, `Preparing`, `Ready`, `Served`, `Completed`, `Cancelled` |
| Bill | `Open`, `PartiallyAllocated`, `Allocated`, `PartiallyPaid`, `Paid`, `Cancelled`, `Reopened` |
| Payment | `Initiated`, `Pending`, `Approved`, `Declined`, `Cancelled`, `Unknown`, `ReconciliationRequired`, `Refunded` |
| FiscalDocument | `Requested`, `Pending`, `Issued`, `Rejected`, `Cancelled`, `Refunded`, `ReconciliationRequired` |
| ProductionBatch | `Planned`, `InProgress`, `Completed`, `Cancelled` |
| PortionReservation | `Reserved`, `Released`, `Consumed`, `Waste` |
| KitchenTicket | `Queued`, `Accepted`, `Preparing`, `Ready`, `Cancelled` |
| KitchenTicketItem | `Queued`, `Preparing`, `Ready`, `Served`, `Cancelled` |
| PrintJob | `Pending`, `Printing`, `Printed`, `Failed`, `Retrying`, `Cancelled` |
| CashSession | `Open`, `Counting`, `Closing`, `Closed`, `Reconciled` |
| MealCardSettlement | `Open`, `Prepared`, `Submitted`, `PartiallySettled`, `Settled`, `Disputed`, `Reconciled` |
| Invoice | `Draft`, `Validating`, `PendingProvider`, `Issued`, `Rejected`, `Cancelled`, `ReconciliationRequired` |
| ReconciliationCase | `Open`, `Investigating`, `WaitingProvider`, `Resolved`, `Dismissed`, `Escalated` |

## Open catalog scope

Discriminator, provider mapping, retry count, additional enum values and
schema migration details remain unapproved. No implementation may create an
internal status that is absent from the source or an approved decision record.

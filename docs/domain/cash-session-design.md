# CashSession Architecture and Contract Design (V1-CSH-001)

**Status:** Approved Architectural Decision Record
**Target Delivery:** V1.3 Cash Management (`V13-CSH-001`, `V13-CSH-002`, `V13-CSH-003`)
**Specification References:** PDF:I.38-I.44, PDF:II.2.7, PDF:II.5.9, PDF:III.9, V0-DOM-001, V0-CMP-002, V1-IAM-002

---

## 1. Context and Problem Statement

In POS restaurant operations, cash drawer management requires strict physical and logical controls to prevent unrecorded
transactions, cash variance leakage, and unauthorized register drawer operations.

Prior to implementing physical database persistence and payment integration in **V1.3**, this document formalizes the
binding contracts, state machines, terminal ownership models, variance formulas, and permission boundaries for
`CashSession`.

---

## 2. Terminal and Cashier Ownership Invariants

1. **Single Active Open Session per Terminal (Invariant CSH-INV-01):**
   - A POS terminal / workstation (`terminal_id`) can have **at most one** cash session in an active state (`Open`,
     `Counting`, or `Closing`) at any given moment.
   - A new session cannot be opened on a terminal until the previous session on that terminal reaches `Closed` or
     `Reconciled` status.

2. **Cashier Assignment and Responsibility (Invariant CSH-INV-02):**
   - Each cash session is initiated by a specific cashier (`cashier_user_id`).
   - If a shift hand-over occurs, the current session MUST be closed (with physical count) and a new session opened by
     the incoming cashier.
   - Starting the count, recording a count and closing are done by that cashier, or by a user holding
     `cash.session.override` (a manager) acting for them (the close rules). The policy enforces it
     (`CashSessionOwnerRequiredException`); a cashier's own actions never ask for the override permission.

---

## 3. CashSession Lifecycle State Machine

```text
              ┌───────────────────────────────────────────────────┐
              │                                                   │
              ▼                                                   │
         [  OPEN  ] ─── (Start Count) ───► [ COUNTING ]           │ (Re-open for adjustment)
              │                                │                  │
              │ (Direct Close with count)      │ (Finalize Count) │
              │                                ▼                  │
              └──────────────────────────► [ CLOSING ] ───────────┘
                                               │
                                               │ (Approve & Close / Supervisor Override)
                                               ▼
                                          [ CLOSED ]
                                               │
                                               │ (Shift Reconciliation / Z-Report)
                                               ▼
                                        [ RECONCILED ]
```

### Canonical State Definitions (`PDF:II.5.9`, `PDF:III.9.1`)

- **`Open`**: The session is active and accepting cash sales, cash-in, cash-out, and cash refunds.
- **`Counting`**: Shift end initiated; cash register is locked from new transactions while physical cash denomination
  counting takes place (`cash_counts`).
- **`Closing`**: Cash counting is complete. The system calculates `expected_cash`, `actual_cash`, and `difference`.
- **`Closed`**: Session is sealed. No further counts or balance modifications are permitted.
- **`Reconciled`**: Daily finance / accounting audit has matched the session totals with fiscal Z-reports and bank
  deposits.

---

## 4. Financial Calculations and Invariants

### Formulas

$$\text{ExpectedCash} = \text{OpeningBalance} + \sum \text{CashIn} + \sum \text{CashSales} - \sum \text{CashOut} - \sum
\text{CashRefunds}$$

$$\text{Difference} = \text{ActualCash} - \text{ExpectedCash}$$

### Invariants

- **`OpeningBalance >= 0`**: Negative opening float is strictly prohibited.
- **`ActualCash >= 0`**: Physical counted cash cannot be negative.
- **Variance Handling (`Difference != 0`)** (PO decisions 2026-11-03):
  - Any non-zero difference needs an explanation (1-500 characters, `difference_explanation`).
  - If $|\text{Difference}| > \text{VarianceToleranceThreshold}$, standard cashier close is blocked; **Supervisor
    approval** (`cash.session.override`) is required with an explicit reason logged to the audit trail. The threshold is
    the manager setting `cash.close_variance_tolerance` (default 50 TL) with a hard ceiling of 500 TL, enforced when the
    value is written and again when it is read.
- **Counted amount**: a close needs at least one recorded count and the closing amount equals the latest count.
- **Unsettled business**: open bills (not `Paid`/`Cancelled`) and payments whose result is unknown (`Pending`,
  `Unknown`, `ReconciliationRequired`) are counted business-wide (bills and payments are not bound to a terminal). They
  never block: the cashier sees the counts and acknowledges them, and the audit event `cash.session.closed` records the
  counts the server saw and the acknowledgement.
- **One transaction**: the close locks the session row, reads the expected cash and the latest count under that lock,
  updates the session, posts the `ClosingDifference` ledger row (only when the difference is not zero) and writes the audit event together; recording a count
  takes the same lock.
- **Reconciliation** is a manager act (`cash.session.override`) and a four-eyes check: the session's cashier and the
  person who closed it do not reconcile it, unless the shared setting `approvals.manager_self_approval_enabled` is on;
  then the record is marked (`reconciled_self_approved`) and the audit event `cash.session.reconciled` says so.
- **Events**: `CashSessionClosedEvent` is returned to the caller and is not placed on the outbox; no module consumes it
  (reconciliation reads `cash.cash_sessions.difference`). The audit event is the record of the close.

---

## 5. Canonical Transaction Types (`PDF:III.9.2`)

1. **`Opening`**: Initial cash float placed into the register drawer at session start.
2. **`Sale`**: Cash payment collected for an order/bill (`related_payment_id`, `related_bill_id`).
3. **`CashIn`**: Mid-shift cash deposit (e.g. additional change float added from main safe).
4. **`CashOut`**: Mid-shift petty cash disbursement (e.g. local vendor cash payment with receipt).

   Manual `CashIn`/`CashOut` rules (V1-RMD-788, PO decision 2026-11-03): both always carry a reason (1-500 characters). A `CashOut` can never be more than the drawer's expected cash; the check and the insert run in one transaction under an exclusive lock on the session row. A `CashOut` above the manager-set threshold (`cash.cash_out_approval_threshold`, strictly greater) is not written to the ledger: it becomes a request that a user with `cash.session.override` approves or rejects (the requester may cancel). Approval re-checks the drawer and writes the `CashOut` row. The requester cannot approve their own request unless `approvals.manager_self_approval_enabled` is on; such an approval is marked `self_approved`. A session with open requests cannot be closed.
5. **`Refund`**: Cash returned to customer for cancelled item or bill refund.
6. **`CountAdjustment`**: Re-count adjustment recorded during counting phase.
7. **`ClosingDifference`**: Final variance of a session, posted to the session's ledger in the closing transaction when
   it is not zero (amount = |difference|, direction `In` for an overage and `Out` for a shortage, note = the cashier's
   explanation). It is an audit entry and never feeds `ExpectedCash`; with it the ledger alone reads
   `expected + signed difference = counted`.

---

## 6. Permission Boundaries (`V1-IAM-002`)

| Action | Allowed Roles | Preconditions |
| :--- | :--- | :--- |
| `OpenSession` | Cashier, Supervisor, Admin | Terminal has NO other active session. Valid opening balance. |
| `RecordTransaction` | Cashier, System (Payment) | Session status is `Open`. |
| `StartCount` | The session's cashier, or a user with `cash.session.override` | Session status is `Open`. |
| `RecordCashCount` | The session's cashier, or a user with `cash.session.override` | Session status is `Counting`. Counted amount >= 0. |
| `CloseSession` | The session's cashier (within tolerance), or a user with `cash.session.override` (any variance) | Session status is `Closing` or `Counting`. A count is recorded and equals the closing amount; any difference is explained. |
| `ReconcileSession` | Users with `cash.session.override` other than the session's cashier and closer (unless self reconciliation is allowed) | Session status is `Closed`. |

Reconciliation is bound to the `cash.session.override` permission, which the supervisor and manager roles hold. The
earlier draft also named Accountant and Admin; no decision grants the Accountant that act, so the document follows the
shipped permission (`129-cash-session-override-permission.up.sql`). Granting it to another role is a role-permission
change, not a code change.

---

## 7. Implementation Map

```text
Policy, snapshot, commands, events   -> CashSessionPolicy (Cash.Contracts)
Session persistence and lifecycle    -> cash.cash_sessions, CashSessionLifecycleService
Append-only ledger                   -> cash.cash_transactions
Cash tender                          -> Cash Tender Command Handler
Manual movements, close, reconcile   -> sections 4 and 5
```

The state machine in section 3 is the one the code runs: a session leaves `Open` only through `StartCount`
(`Open` -> `Counting`), closes from `Counting`, and `Closing` is accepted as a close source but no command enters it
today.

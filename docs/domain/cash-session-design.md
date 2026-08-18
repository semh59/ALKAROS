# CashSession Architecture and Contract Design (V1-CSH-001)

**Status:** Approved Architectural Decision Record  
**Target Delivery:** V1.2 Cash Management (`V12-CSH-001`, `V12-CSH-002`, `V12-CSH-003`)  
**Specification References:** PDF:I.38-I.44, PDF:II.2.7, PDF:II.5.9, PDF:III.9, V0-DOM-001, V0-CMP-002, V1-IAM-002

---

## 1. Context and Problem Statement

In POS restaurant operations, cash drawer management requires strict physical and logical controls to prevent unrecorded transactions, cash variance leakage, and unauthorized register drawer operations.

Prior to implementing physical database persistence and payment integration in **V1.2**, this document formalizes the binding contracts, state machines, terminal ownership models, variance formulas, and permission boundaries for `CashSession`.

---

## 2. Terminal and Cashier Ownership Invariants

1. **Single Active Open Session per Terminal (Invariant CSH-INV-01):**
   - A POS terminal / workstation (`terminal_id`) can have **at most one** cash session in an active state (`Open`, `Counting`, or `Closing`) at any given moment.
   - A new session cannot be opened on a terminal until the previous session on that terminal reaches `Closed` or `Reconciled` status.

2. **Cashier Assignment and Responsibility (Invariant CSH-INV-02):**
   - Each cash session is initiated by a specific cashier (`cashier_user_id`).
   - If a shift hand-over occurs, the current session MUST be closed (with physical count) and a new session opened by the incoming cashier.

---

## 3. CashSession Lifecycle State Machine

```
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

### Canonical State Definitions (`PDF:II.5.9`, `PDF:III.9.1`):
- **`Open`**: The session is active and accepting cash sales, cash-in, cash-out, and cash refunds.
- **`Counting`**: Shift end initiated; cash register is locked from new transactions while physical cash denomination counting takes place (`cash_counts`).
- **`Closing`**: Cash counting is complete. The system calculates `expected_cash`, `actual_cash`, and `difference`.
- **`Closed`**: Session is sealed. No further counts or balance modifications are permitted.
- **`Reconciled`**: Daily finance / accounting audit has matched the session totals with fiscal Z-reports and bank deposits.

---

## 4. Financial Calculations and Invariants

### Formulas:
$$\text{ExpectedCash} = \text{OpeningBalance} + \sum \text{CashIn} + \sum \text{CashSales} - \sum \text{CashOut} - \sum \text{CashRefunds}$$

$$\text{Difference} = \text{ActualCash} - \text{ExpectedCash}$$

### Invariants:
- **`OpeningBalance >= 0`**: Negative opening float is strictly prohibited.
- **`ActualCash >= 0`**: Physical counted cash cannot be negative.
- **Variance Handling (`Difference != 0`)**:
  - If $|\text{Difference}| > \text{VarianceToleranceThreshold}$ (e.g. 50.00 TL), standard cashier close is blocked; **Supervisor approval** (`ManagerForceCloseSession` / `CashierSupervisorOverride`) is required with an explicit reason logged to audit trail.

---

## 5. Canonical Transaction Types (`PDF:III.9.2`)

1. **`Opening`**: Initial cash float placed into the register drawer at session start.
2. **`Sale`**: Cash payment collected for an order/bill (`related_payment_id`, `related_bill_id`).
3. **`CashIn`**: Mid-shift cash deposit (e.g. additional change float added from main safe).
4. **`CashOut`**: Mid-shift petty cash disbursement (e.g. local vendor cash payment with receipt).
5. **`Refund`**: Cash returned to customer for cancelled item or bill refund.
6. **`CountAdjustment`**: Re-count adjustment recorded during counting phase.
7. **`ClosingDifference`**: Final variance posted to general ledger on session closure.

---

## 6. Permission Boundaries (`V1-IAM-002`)

| Action | Allowed Roles | Preconditions |
| :--- | :--- | :--- |
| `OpenSession` | Cashier, Supervisor, Admin | Terminal has NO other active session. Valid opening balance. |
| `RecordTransaction` | Cashier, System (Payment) | Session status is `Open`. |
| `StartCount` | Cashier, Supervisor | Session status is `Open`. |
| `RecordCashCount` | Cashier, Supervisor | Session status is `Counting`. Counted amount >= 0. |
| `CloseSession` | Cashier (within tolerance), Supervisor (any variance) | Session status is `Closing` or `Counting`. |
| `ReconcileSession` | Supervisor, Accountant, Admin | Session status is `Closed`. |

---

## 7. Downstream Dependency Roadmap (Why Implementation Waits for V1.2)

```
V1-CSH-001 (Design & Contracts) [Current Task]
       │
       ▼
V1.2 Implementation Milestone:
       ├── V12-CSH-001: Cash Session Table & Repository Persistence
       ├── V12-CSH-002: Cash Transaction Immutable Ledger
       ├── V12-CSH-003: Cash Tender Command Handler
       └── V12-PAY-001: Integration with Multi-Tender Payment Aggregate
```

**Rationale:** The execution of cash transactions requires atomic coordination with the **Payments Aggregate** (`V12-PAY-001`) and fiscal printer closure gates (`V12-FSC-002`). Implementing database tables prematurely in V1 without the payment executor would create orphan state and risk divergence.

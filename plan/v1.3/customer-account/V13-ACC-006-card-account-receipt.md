# V13-ACC-006 - Implement card customer-account receipt

- Task ID: V13-ACC-006
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.33
- PDF:II.2.15-II.2.16
- PDF:II.3.11-II.3.12
- PDF:III.18.3
- CORR:C23

## Goal

Bill'den bağımsız BankCard AccountPayment'i doğrulanmış Hugin sonucuyla crash-safe tamamlamak ve Approved sonucu tek
Payment AccountTransaction'a bağlamak.

## Owned surface

- `src/Modules/CustomerAccounts/CardReceipts/**`, `tests/Modules/CustomerAccounts/CardReceipts/**`,
  `database/migrations/V13/V13-ACC-006/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Durable orchestration, bill-null Payment link, Hugin correlation, Approved/Declined/Unknown mapping, resume, duplicate
  suppression, AccountTransaction posting, overpayment policy ve balance projection update.

## Out of scope

- Hugin protocol/transport, Bill PaymentAllocation, fiscal Bill closure ve ReconciliationCase persistence.

## Dependencies

- V13-ACC-001
- V13-ACC-002
- V13-ACC-004
- V12-HUG-001
- V12-HUG-002
- V12-PAY-001
- V0-DOM-007
- V1-FND-005
- V1-FND-006
- V1-SEC-002

## Deliverables

- Card customer-account receipt durable workflow'u, migration ve her crash noktası için resume/idempotency tests.

## Acceptance evidence

- Approved terminal reference bir Payment, bir AccountPayment ve bir Payment AccountTransaction'a bağlanır; retry
  ikinci provider charge, Payment, AccountTransaction, Bill veya PaymentAllocation üretmez.
- Declined bakiyeyi değiştirmez; Unknown success sayılmaz, yeniden tahsilata açılamaz ve `V13-ACC-007` için aynı typed
  divergence evidence'ini idempotent üretir.

## Handoff

- V13-ACC-007
- V13-UI-001

# V13-ACC-005 - Implement cash customer-account receipt

- Task ID: V13-ACC-005
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.30-I.33
- PDF:II.2.15
- PDF:II.3.11
- PDF:III.18.3
- CORR:C23

## Goal

Açık CashSession içinde Bill'den bağımsız Payment, cash AccountPayment, CashTransaction ve Payment AccountTransaction
kayıtlarını tek database transaction içinde oluşturmak.

## Owned surface

- `src/Modules/CustomerAccounts/CashReceipts/**`, `tests/Modules/CustomerAccounts/CashReceipts/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Açık session kontrolü, positive amount/currency, idempotency, CashTransaction source link, account overpayment policy,
  AccountPayment approval ve balance projection update.

## Out of scope

- Bill PaymentAllocation, change verme, BankCard/Hugin transport ve CashSession lifecycle.

## Dependencies

- V13-ACC-001
- V13-ACC-002
- V13-ACC-004
- V12-CSH-001
- V12-CSH-002
- V12-PAY-001
- V0-DOM-007
- V1-FND-005

## Deliverables

- Cash customer-account receipt production code'u ve task-specific automated transaction/idempotency tests.

## Acceptance evidence

- Success tam olarak bir Approved Payment, bir Approved AccountPayment, bir CashTransaction ve bir Payment
  AccountTransaction üretir; dördü aynı transaction içinde commit edilir veya hiçbiri yazılmaz.
- Closed session, invalid amount/currency, duplicate source veya policy rejection bakiyeyi değiştirmez; bu akış Bill ve
  PaymentAllocation oluşturmaz.

## Handoff

- V13-ACC-007
- V13-UI-001

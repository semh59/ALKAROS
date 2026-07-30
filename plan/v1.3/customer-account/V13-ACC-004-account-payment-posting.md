# V13-ACC-004 - Implement AccountPayment aggregate

- Task ID: V13-ACC-004
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

Bill'den bağımsız tahsilatın kimliğini, method'unu, amount'unu ve durable Requested/Approved/Declined/Unknown durum
geçişlerini AccountPayment aggregate'ında kalıcılaştırmak.

## Owned surface

- `src/Modules/CustomerAccounts/AccountPayments/**`, `tests/Modules/CustomerAccounts/AccountPayments/**`,
  `database/migrations/V13/V13-ACC-004/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Bill-null invariant, customer account, positive amount/currency, Cash/BankCard method, idempotency key, provider/cash
  reference uniqueness, status history ve audit.

## Out of scope

- CashSession/CashTransaction yazımı, Hugin transport, AccountTransaction posting ve PaymentAllocation.

## Dependencies

- V13-ACC-001
- V0-DOM-007
- V0-DAT-002

## Deliverables

- `src/Modules/CustomerAccounts/AccountPayments/**` aggregate, persistence contract ve task-specific automated tests.
- Yalnız bu task'a ait ileri/geri migration.

## Acceptance evidence

- Aynı idempotency key bir AccountPayment üretir; aynı provider/cash reference ikinci aggregate'a bağlanamaz.
- `Approved` yalnız method-specific owner'ın doğrulanmış evidence referansıyla oluşur; `Unknown` success sayılmaz ve bu
  task AccountTransaction, CashTransaction veya PaymentAllocation yazmaz.

## Handoff

- V13-ACC-005
- V13-ACC-006

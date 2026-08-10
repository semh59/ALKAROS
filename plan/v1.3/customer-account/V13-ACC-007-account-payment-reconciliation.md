# V13-ACC-007 - Implement account-payment reconciliation

- Task ID: V13-ACC-007
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.30-I.33
- PDF:II.2.15
- PDF:II.2.21
- PDF:III.18.3
- PDF:III.23
- CORR:C23

## Goal

AccountPayment, cash/provider evidence ve AccountTransaction kaynakları farklılaştığında tekilleştirilmiş
ReconciliationCase oluşturmak.

## Owned surface

- `src/Modules/Reconciliation/AccountPayments/**`, `tests/Modules/Reconciliation/AccountPayments/**`,
  `database/migrations/V13/V13-ACC-007/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Unknown card result, Approved-without-account-posting, cash/account mismatch, duplicate reference ve append-only
  resolution audit.

## Out of scope

- Provider status query, AccountPayment/AccountTransaction mutation, Bill payment ve unified dashboard UI.

## Dependencies

- V13-ACC-005
- V13-ACC-006
- V12-HUG-002

## Deliverables

- Account-payment ReconciliationCase producer, migration ve duplicate/concurrency/resolution automated tests.

## Acceptance evidence

- Aynı unresolved divergence tek açık case üretir; case iki authoritative tarafı ve evidence reference'larını taşır.
- Resolution önceki finansal kayıtları değiştirmez; yeni append-only audit sonucu üretir.

## Handoff

- V13-UI-001
- V15-REC-001

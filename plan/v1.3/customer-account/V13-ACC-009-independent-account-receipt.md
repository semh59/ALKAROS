# V13-ACC-009 - Record independent account receipts

- Task ID: V13-ACC-009
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

Bill'den bağımsız bir AccountReceipt'i, onu doğrulayan AccountPayment'a bağlayarak kaydetmek; Bill veya
PaymentAllocation oluşturmamak.

## Owned surface

- `src/Modules/CustomerAccounts/AccountReceipts/**`
- `tests/Modules/CustomerAccounts/AccountReceipts/**`
- `database/migrations/V13/V13-ACC-009/**`
- Bu görev, CashReceipts, CardReceipts, AccountPayments, AccountTransaction veya PaymentAllocation owned surface'ini
  değiştiremez.

## In scope

- AccountReceipt kimliği, customer account, AccountPayment referansı, amount/currency ve append-only audit kaydı.
- Aynı receipt idempotency key'i veya aynı doğrulanmış AccountPayment için tek receipt; retry ikinci receipt üretmez.
- Unknown veya uyuşmayan AccountPayment sonucu receipt olarak kapanmaz; V13-ACC-007 için typed reconciliation evidence
  üretir.

## Out of scope

- Cash/card tahsilat transportu, AccountPayment state geçişi, AccountTransaction posting, CashTransaction, Bill ve
  PaymentAllocation.

## Dependencies

- V13-ACC-001
- V13-ACC-004

## Deliverables

- AccountReceipt source/persistence contract'ı, task-specific automated testler ve yalnız bu göreve ait ileri/geri
  migration.

## Acceptance evidence

- Aynı doğrulanmış AccountPayment ve idempotency key için retry dahil yalnız bir AccountReceipt kalır; Bill veya
  PaymentAllocation oluşmaz.
- Unknown, duplicate veya AccountPayment ile amount/currency uyuşmayan istek receipt olarak tamamlanmaz ve aynı
  uyuşmazlık V13-ACC-007 tarafından tek reconciliation case'e bağlanabilecek typed evidence taşır.
- Migration ileri/geri kanıtı ve dedup/retry/reconciliation testleri exit code 0 üretir.

## Handoff

- V13-ACC-007
- V13-UI-001

# V14-ACC-004 - Implement AccountPayment aggregate

- Task ID: V14-ACC-004
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.30-I.33
- PDF:II.2.15
- PDF:II.3.11
- PDF:III.18.3
- CORR:C23

## Goal

AccountPayment'ın kimliğini, method'unu, amount'unu ve durable Requested/Approved/Declined/Unknown durum geçişlerini
kalıcılaştırmak; bağımsız AccountReceipt'i veya Bill allocation'ını bu aggregate'a yüklememek.

## Owned surface

- `src/Modules/CustomerAccounts/AccountPayments/**`, `tests/Modules/CustomerAccounts/AccountPayments/**`,
  `database/migrations/V14/V14-ACC-004/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/CustomerAccounts/CustomerAccountsModule.cs (V14-ACC-001
  sahipliğinde) — yalnız AccountPayment deposunun kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx (V0-GOV-040 sahipliğinde) — yalnız bu görevin test projesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs —
  yalnız bu görevin migration'ı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/unreachable_services_allowlist.json (V1-RMD-272
  sahipliğinde) — yalnız bu görevin HTTP yüzeyi henüz olmayan tipleri
- Bu görev, başka bir task'ın owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Customer account, positive amount/currency, Cash/BankCard method, idempotency key, provider/cash reference
  uniqueness, status history ve audit.

## Out of scope

- AccountReceipt oluşturma, Bill allocation, CashSession/CashTransaction yazımı, Hugin transport, AccountTransaction
  posting ve PaymentAllocation.

## Dependencies

- V14-ACC-001
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

- V14-ACC-005
- V14-ACC-006
- V14-ACC-009

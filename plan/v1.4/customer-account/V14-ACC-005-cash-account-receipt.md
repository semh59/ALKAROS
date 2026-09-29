# V14-ACC-005 - Implement cash customer-account receipt

- Task ID: V14-ACC-005
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

Açık CashSession içinde Bill'den bağımsız Payment, cash AccountPayment, CashTransaction ve Payment AccountTransaction
kayıtlarını tek database transaction içinde oluşturmak.

## Owned surface

- `src/Modules/CustomerAccounts/CashReceipts/**`, `tests/Modules/CustomerAccounts/CashReceipts/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/CustomerAccounts/ALKAROS.CustomerAccounts.csproj
  (V14-ACC-001 sahipliğinde) — yalnız `ALKAROS.Cash` proje referansı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Modules/ModuleRegistry.cs,
  tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs ve
  tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs (V1-FND-001 sahipliğinde) — yalnız yeni
  `CustomerAccounts.CashReceipts` modülünün kaydı, onaylı kenarları ve modül sayısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/module-dependency-rules.md (V0-ARC-001 sahipliğinde) —
  yalnız yeni satır 32
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/packages.lock.json, src/Modules/CustomerAccounts/packages.lock.json
  ve tests/**/packages.lock.json — yalnız yeni `ALKAROS.Cash` referansının kilit dosyalarındaki
  `alkaros.customeraccounts` girdisine yansıması (`dotnet restore --force-evaluate`)
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx (V0-GOV-040 sahipliğinde) — yalnız bu görevin test projesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/unreachable_services_allowlist.json (V1-RMD-272
  sahipliğinde) — yalnız bu görevin HTTP yüzeyi henüz olmayan tipleri
- Bu görev, başka bir task'ın owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Açık session kontrolü, positive amount/currency, idempotency, CashTransaction source link, account overpayment policy,
  AccountPayment approval ve balance projection update.
- Uygulama kararları (V1-RMD-441, 2026-09-29):
  - "Bill'den bağımsız Payment" V14-ACC-004'ün Approved `AccountPayment` kaydıdır. `payments.payments` adisyona bağlıdır
    (`bill_id NOT NULL`, V13-PAY-001) ve gün sonu cirosu ile ödeme raporları onaylı `payments.payments` satırlarını
    sayar (V1-RMD-421). Cariye yazılan borç, adisyonda zaten bir onaylı Payment üretmiştir (V14-ACC-003); tahsilat için
    ikinci bir `payments.payments` satırı aynı satışı iki kez ciro sayardı.
  - Kasa hareketi `CashIn` tipindedir (kasaya giren nakit, beklenen kasaya eklenir); notu tahsilatı, idempotency anahtarı
    `account-payment:{id}` ödemeyi gösterir. Yeni bir kasa hareketi tipi V13-CSH-002 şemasını değiştirirdi.
  - Fazla ödeme politikası: tahsilat, müşterinin o anki cari borcunu aşamaz (önceden ödeme/alacak açılmaz); aşarsa ret.

## Out of scope

- Bill PaymentAllocation, change verme, BankCard/Hugin transport ve CashSession lifecycle.

## Dependencies

- V14-ACC-001
- V14-ACC-002
- V14-ACC-004
- V13-CSH-001
- V13-CSH-002
- V13-PAY-001
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

- V14-ACC-007
- V14-UI-001

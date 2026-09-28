# V14-ACC-003 - Implement CustomerAccount tender posting

- Task ID: V14-ACC-003
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.30-I.33
- PDF:II.2.15
- PDF:II.3.11
- PDF:III.18

## Goal

Onaylanmış bir CustomerAccount tender'ını çift kayıt oluşturmadan tek AccountCharge ve PaymentAllocation kaydına
dönüştürmek.

## Owned surface

- `src/Modules/CustomerAccounts/BillCharges/**`, `tests/Modules/CustomerAccounts/BillCharges/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/CustomerAccounts/ALKAROS.CustomerAccounts.csproj
  (V14-ACC-001 sahipliğinde kalır) — yalnız `ALKAROS.Payments`/`ALKAROS.CustomerData` referansları eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/CustomerAccounts/TransactionLedger/IAccountTransactionLedger.cs,
  src/Modules/CustomerAccounts/TransactionLedger/PostgresAccountTransactionLedger.cs,
  tests/Modules/CustomerAccounts/TransactionLedger/PostgresAccountTransactionLedgerTests.cs
  (V14-ACC-001 sahipliğinde kalır) — yalnız `IPaymentRepository.AddAsync`'in birebir aynı deseniyle
  bir `(connection, transaction)` overload'ı eklenir (bu görevin Payment+Allocation ile atomik
  yazması için zorunlu); mevcut davranış değişmedi (yeni overload'a delege ediyor).
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Modules/ModuleRegistry.cs
  (V1-FND-001 sahipliğinde kalır) — yalnız `CustomerAccountsBillChargesModule` `DefaultCatalog`'a eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx (V0-GOV-040 sahipliğinde kalır) — yalnız
  yeni bir `<Project Path>` girdisi eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/module-dependency-rules.md
  (V0-ARC-001 sahipliğinde kalır) — yalnız yeni satır 31 eklenir (satır 16'nın onaylı Bill/Payment
  kenarlarını DEĞİŞTİRMEZ, yalnız ilk kez kullanan ayrı bir alt-modül satırı ekler).
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs
  (V1-FND-001 sahipliğinde kalır) — yalnız `ApprovedEdges["CustomerAccounts.BillCharges"]` eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs
  (V1-FND-001 sahipliğinde kalır) — yalnız sabit modül sayısı `35` → `36` değişir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/unreachable_services_allowlist.json
  (V1-RMD-278 sahipliğinde kalır) — yalnız bu görevin 4 yeni tipi eklenir (bilinçli olarak hiçbir
  HTTP endpoint eklenmiyor — bir kasiyer ekranına "hesaba yaz" seçeneği eklendiğinde eklenecek).
- Bu görev, başka bir task'ın owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `AccountChargeHandler`: eligibility (müşteri var mı ve anonimleştirilmemiş mi —
  `ICustomerProfileStore`), credit-policy sonucu (`ICustomerCreditPolicy` — genel credit scoring
  kapsam dışı olduğu için `AlwaysApproveCreditPolicy` dürüst bir yer tutucu), `AccountCharge`
  source (V14-ACC-001'in `IAccountTransactionLedger`'ı, `Charge` tipi, `SourceReferenceType="Payment"`),
  Payment approval (`Payment.Tender().Approve()`, tenders=approved=AmountDue — hiç para üstü yok)
  ve allocation transaction boundary (`ALKAROS.Cash.TenderHandler.CashTenderHandler`'ın BİREBİR AYNI
  deseni: tek bir `NpgsqlTransaction` içinde Payment + AccountTransaction + PaymentAllocation, ya
  hepsi commit olur ya hiçbiri; idempotency key üzerinde `pg_advisory_xact_lock`; tekrar denemede
  var olan allocation'ı bulup replay eder, ikinci bir kayıt oluşturmaz).
- Satır 16'nın (Customer Account) önceden onaylı Bill/Payment kenarları BU görevle ilk kez
  gerçekten kullanılıyor.

## Out of scope

- Periodic Invoice issuance ve genel credit scoring.

## Dependencies

- V1-FND-005
- V14-ACC-001
- V13-PAY-002
- V13-ALC-001
- V0-DOM-007

## Deliverables

- `src/Modules/CustomerAccounts/BillCharges/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Retry bir ücret/tahsis üretir; yetersiz politika onayı ne Bill'yi ne de hesabı değiştirmez; miktarlar eşit kalır.
- `dotnet test tests/Modules/CustomerAccounts/BillCharges/ALKAROS.CustomerAccounts.BillCharges.Tests.csproj`
  (gerçek Postgres'e karşı — gerçek Catalog/Table/Order/Bill/Payment/PaymentAllocation/
  CustomerProfile/AccountTransaction zinciri, `ALKAROS.Cash.TenderHandler.Tests.
  CashTenderHandlerTests`'in birebir aynı tohumlama tekniğiyle): 7/7 geçti. Kapsanan: Payment +
  PaymentAllocation + AccountTransaction (Charge, Debit yönü) atomik olarak oluşuyor; aynı
  idempotency key ile tekrar deneme üçünü de birebir replay ediyor (ikinci kayıt yok);
  anonimleştirilmiş bir müşteriye charge reddediliyor VE ne Bill ne hesap değişiyor; bilinmeyen
  müşteri hiçbir şeye dokunmadan reddediliyor; credit-policy reddi ne Bill'i ne hesabı değiştiriyor;
  bilinmeyen Bill eligibility'den SONRA ama hiçbir yazımdan ÖNCE reddediliyor; aşırı tahsis
  (`OverAllocationException`) hiçbir şey yazmadan reddediliyor.
- V14-ACC-001'in kendi ledger'ına eklenen `(connection, transaction)` overload'ı için 2 yeni test
  (`tests/Modules/CustomerAccounts/TransactionLedger`): harici bir transaction commit edilirse satır
  kalıcı oluyor, rollback edilirse hiçbir şey kalmıyor — V14-ACC-001'in kendi 42 testi + bu 2 yeni
  test toplam 44/44 geçti (regresyon yok).
- `dotnet test tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj`: 9/9 geçti
  (`CustomerAccounts.BillCharges` → `CustomerAccounts`/`CustomerData`/`Payments`/
  `Payments.Allocations.Persistence`/`Billing` kenarları onaylandı).
- `dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj` (hedefli, sistemin
  bellek baskısı altında olması nedeniyle yalnızca sayıma duyarlı test): 1/1 geçti (36 modül; bu
  görev yeni bir migration eklemedi).
- `dotnet build src/Modules/CustomerAccounts/ALKAROS.CustomerAccounts.csproj` ve
  `dotnet build src/Host/ALKAROS.Host.csproj`: 0 hata.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `python tools/project-manifest/project_manifest_tool.py` → `VALID (0 differences across
  Solution, Disk, and ProjectReferences)`.

## Handoff

- V14-INV-001
- V14-ACC-008

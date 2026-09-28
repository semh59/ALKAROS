# V14-ACC-001 - Implement CustomerAccount transaction ledger

- Task ID: V14-ACC-001
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.30-I.33
- PDF:II.2.15
- PDF:II.3.11
- PDF:III.18
- CORR:C3

## Goal

Açık yön semantiği ve değişmez kaynak bağlantılarıyla pozitif büyüklükteki hesap işlemlerini sürdürün.

## Owned surface

- `src/Modules/CustomerAccounts/ALKAROS.CustomerAccounts.csproj`,
  `src/Modules/CustomerAccounts/CustomerAccountsModule.cs`,
  `src/Modules/CustomerAccounts/TransactionLedger/**`, `tests/Modules/CustomerAccounts/TransactionLedger/**`,
  `database/migrations/V14/V14-ACC-001/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Modules/ModuleRegistry.cs
  (V1-FND-001 sahipliğinde kalır) — yalnız `CustomerAccountsModule` `DefaultCatalog`'a eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/ALKAROS.Host.csproj
  (V1-FND-001 sahipliğinde kalır) — yalnız yeni `ProjectReference` eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx (V0-GOV-040 sahipliğinde kalır) —
  yalnız yeni iki `<Project Path>` girdisi eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
  (V1-IAM-025 ailesinin sahipliğinde kalır) — yalnız `161` girdisi eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
  (V1-FND-004 sahipliğinde kalır) — yalnız `PhaseBMax` `"160"` → `"161"` değişir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
  ve tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs
  (V1-FND-004/V1-FND-001 sahipliğinde kalır) — yalnız sabit sayılar güncellenir (dosyaların
  kendi yorumu, her yeni modül/migration'ın bunu güncellemesini zaten gerektiriyor).
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs
  (V1-FND-001 sahipliğinde kalır) — yalnız `ModuleAssemblies`'e `ALKAROS.CustomerAccounts` eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/consistency_audit.py
  (V1-RMD-xxx ailesinin sahipliğinde kalır) — yalnız `MODULE_SCHEMA`'ya `CustomerAccounts` eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/unreachable_services_allowlist.json
  (V1-RMD-278 sahipliğinde kalır) — yalnız bu görevin 2 yeni tipi eklenir (bilinçli olarak hiçbir
  HTTP endpoint eklenmiyor, V14-CST-001/002 ile aynı gerekçe).
- Bu görev, başka bir task'ın owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `AccountTransactionType` (V0-DOM-007'nin 7 kanonik tipi): `Charge`, `Payment`, `Invoice`, `Credit`,
  `Debit`, `Adjustment`, `Refund`.
- `AccountTransactionDirectionRules`: `Charge`/`Invoice`/`Debit` → `Debit`; `Payment`/`Credit`/`Refund`
  → `Credit`; `Adjustment` → yönü yok, işareti `amount`'ın kendisinde taşınır.
- `RecordAccountTransactionRequest`: her davetin kendi kendini doğrulaması — sabit yönlü 6 tip için
  `amount` negatif olamaz; negatif bir Adjustment için not VE oluşturan zorunlu (V0-DOM-007
  invariant 5).
- `IAccountTransactionLedger`/`PostgresAccountTransactionLedger`: yalnızca ekleme (append-only) —
  arayüzde hiçbir update/delete metodu yok; veritabanı seviyesinde de bir tetikleyici (trigger)
  UPDATE/DELETE'i reddediyor (audit.audit_events'in aynı deseni). `direction` uygulama tarafından
  hiç yazılmıyor — Postgres'in kendi `GENERATED ALWAYS AS` sütunu tarafından türetiliyor
  (V0-DOM-007: "direction uygulama tarafından asla yazılmaz"). İdempotency:
  (customer_id, transaction_type, source_reference_type, source_reference_id) benzersiz anahtarı —
  aynı kaynak olayın tekrar denenmesi yeni bir satır oluşturmaz, var olanı döner.
- `CustomerAccountsModule : IModule` — bağımsız (leaf) modül; row 16'nın (Customer Account)
  önceden onaylı Bill/Payment kenarları bu görevde HENÜZ kullanılmıyor (bkz. Goal'daki not).
- Migration 161 (`customer_account.account_transactions`).
- Bilinçli olarak HİÇBİR HTTP endpoint eklenmedi — V14-CST-001/002 ile aynı gerekçe.

## Out of scope

- Önbelleğe alınmış mevcut bakiye ve invoice oluşturma.

## Dependencies

- V14-CST-001
- V0-DAT-002
- V0-DOM-007

## Deliverables

- `src/Modules/CustomerAccounts/TransactionLedger/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Her işlem türünün bir imzalı etkisi vardır; geçersiz işaret/tür kombinasyonları ve yerinde düzenlemeler reddedilir.
- `dotnet test tests/Modules/CustomerAccounts/TransactionLedger/ALKAROS.CustomerAccounts.TransactionLedger.Tests.csproj`
  (gerçek Postgres'e karşı): 42/42 geçti. Kapsanan: her sabit-yönlü tip için doğru yön; Adjustment'ın
  yönü `amount` işaretini takip eder; karar kaydının kendi 3 örnek hesabı (100+150-80=170;
  -20 ayarlamasıyla 150; +30 iadeyle 120) birebir yeniden üretildi; veritabanının GERÇEKTEN
  `direction`'ı türettiği (uygulama hiç göndermiyor) kanıtlandı; aynı kaynak olayın tekrarı
  idempotent'tir (yeni satır oluşturmaz); aynı kaynak id'si farklı bir işlem tipiyle gerçek ayrı bir
  satırdır; append-only tetikleyici ham bir UPDATE/DELETE'i reddeder; veritabanı, uygulama katmanı
  atlanarak bile negatif tutarı/nedensiz negatif ayarlamayı CHECK kısıtlarıyla reddeder.
- `dotnet test tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj`: 9/9 geçti
  (`DependsOn` boş olduğu için `ApprovedEdges`'e yeni bir satır gerekmedi).
- `dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj` (hedefli, sistemin bellek
  baskısı altında olması nedeniyle yalnızca sayıma duyarlı iki test): 2/2 geçti (35 modül, 160
  migration).
- `dotnet build src/Modules/CustomerAccounts/ALKAROS.CustomerAccounts.csproj` ve
  `dotnet build src/Host/ALKAROS.Host.csproj`: 0 hata.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `python tools/project-manifest/project_manifest_tool.py` → `VALID (0 differences across
  Solution, Disk, and ProjectReferences)`.

## Handoff

- V14-ACC-002
- V14-ACC-003
- V14-ACC-004

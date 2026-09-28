# V14-CST-001 - Implement customer PII boundary

- Task ID: V14-CST-001
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

Field-level access policy ile PII sahibi boundary içinde minimum customer identity, tax ve contact alanlarını
kalıcılaştırmak.

**Taslak notu (2026-09-18, `V14-GOV-001`):** `GATE-V14-ENTRY` hâlâ açık
olduğu için bu görev başlangıçta `InProgress` alınamamıştı. Semih'in
onayıyla, gerçek gate kapanmadan önce yalnız bir domain taslağı
`evidence/V14-GOV-001/customer-pii-draft/` altında yazılmıştı (bu görevin
Owned surface'ının DIŞINDA, ayrı/standalone bir proje olarak).

**2026-09-28 (Faz 4 başlangıcı):** `GATE-V14-ENTRY` artık kapalı sayılıyor
(`GATE-V13-EXIT` waiver'ı, `plan/GATES.md`'nin V13_EXIT_ENTRY_WAIVER
tablosu) ve `V0-CMP-003` `Done`, bu yüzden görev gerçekten başladı.
Taslak birebir referans alındı: `CustomerProfile`/erişim politikası/
retention guard aynı şekilde taşındı, ama artık GERÇEK bir Postgres
store'a bağlı (`customer_data.profiles`, migration 159) ve name/phone/
email/address `ALKAROS.SensitiveData` zarfıyla (AES-256-GCM,
`SensitiveCategory.Pii`) şifreleniyor — `ALKAROS.Invoicing.Qnb.
CredentialRegistration.PostgresQnbCredentialStore`'un birebir aynı
deseni (kendi private resolver/cipher/protector zinciri,
`ISensitiveDataAccessPolicy`'nin paylaşılan DI kaydı yok).

## Owned surface

- `src/Modules/CustomerData/Profiles/**`, `tests/Modules/CustomerData/Profiles/**`,
  `database/migrations/V14/V14-CST-001/**`
- `src/Modules/CustomerData/ALKAROS.CustomerData.csproj`, `src/Modules/CustomerData/CustomerDataModule.cs`
  — bu iki dosya modül-köküdür, bu görev tarafından ilk kez oluşturuldu; `V14-CST-002`
  (`AnonymizationState/**`) aynı modülün parçası olarak bunlara "Sınırlı ek" şeklinde dokunacak.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Modules/ModuleRegistry.cs
  (V1-FND-001 sahipliğinde kalır) — yalnız `CustomerDataModule` `DefaultCatalog`'a eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/ALKAROS.Host.csproj
  (V1-FND-001 sahipliğinde kalır) — yalnız yeni `ProjectReference` eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx
  (V0-GOV-040 sahipliğinde kalır) — yalnız yeni iki `<Project Path>` girdisi eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
  (V1-IAM-025 ailesinin sahipliğinde kalır) — yalnız `159` girdisi eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
  (V1-FND-004 sahipliğinde kalır) — yalnız `PhaseBMax` `"158"` → `"159"` değişir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/module-dependency-rules.md
  (V0-ARC-001 sahipliğinde kalır) — yalnız yeni satır 30 (Customer Data) eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/consistency_audit.py
  (V1-RMD-xxx ailesinin sahipliğinde kalır) — yalnız `MODULE_SCHEMA`'ya `CustomerData` eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/unreachable_services_allowlist.json
  (V1-RMD-278 sahipliğinde kalır) — yalnız `ICustomerProfileStore`/`PostgresCustomerProfileStore`
  girdileri eklenir (bu görev bilinçli olarak hiçbir HTTP endpoint eklemiyor — bkz. In scope).
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs
  (V1-FND-001 sahipliğinde kalır) — yalnız `ModuleAssemblies`'e `ALKAROS.CustomerData` eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs
  (V1-FND-001 sahipliğinde kalır) — yalnız sabit modül sayısı `33` → `34` değişir (dosyanın kendi
  geçmişi, her yeni modülün bu sayıyı güncellemesini zaten gerektiriyor).
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
  (V1-FND-004 sahipliğinde kalır) — yalnız sabit migration sayısı `157` → `158`, `RuntimeManifestIds`'e
  `"159"`, `LastEntryTables`'a `"profiles"` eklenir (dosyanın kendi yorumu, "gone stale THREE times",
  her yeni migration'ın bunu güncellemesini zaten gerektiriyor).
- Bu görev, başka bir task'ın owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `CustomerProfile`/`CustomerAccessRole`/`CustomerProfileAccessPolicy`/`CustomerProfileRetention`:
  V0-CMP-003'ün "Customer PII" satırıyla birebir eşleşen alan seti (name/phone/email/address),
  10 yıllık saklama, Cashier+Manager okuma yetkisi, diğer her rol için tam redaksiyon (kısmi
  sızıntı yok), anonimleştirilmiş kayda fatura kesme guard'ı (`CustomerProfileAnonymizedException`).
- `ICustomerProfileStore`/`PostgresCustomerProfileStore`: `CreateAsync`/`GetAsync` (rol projeksiyonu
  ile)/`UpdateContactAsync`/`AnonymizeAsync` — name/phone/email/address `ALKAROS.SensitiveData`
  zarfıyla (`SensitiveCategory.Pii`) tek bir zarfta şifrelenir; anonimleştirme, çözülemeyen bir
  sentinel zarfla üzerine yazar (`PostgresRetentionSubjectStore`'un birebir aynı deseni).
  Optimistic concurrency (`row_version`) her mutasyonda kontrol edilir;
  `CustomerProfileConcurrencyException`/`CustomerProfileNotFoundException` tipik hatalar.
  Anonimleştirme idempotent: kayıt zaten anonimleştirilmişse ikinci çağrı hata vermez.
- `CustomerDataModule : IModule` — bağımsız (leaf) modül, `ModuleRegistry.DefaultCatalog`'a eklendi.
- Migration 159 (`customer_data.profiles`).
- Bilinçli olarak HİÇBİR HTTP endpoint eklenmedi — bu görevin kapsamı yalnız domain+persistence
  sınırı; bir endpoint, müşteri kimliğine ihtiyaç duyan ilk gerçek tüketici (V14-ACC/V14-INV/
  V14-UI) geldiğinde eklenecek (bkz. `unreachable_services_allowlist.json` girdisi).
- Anonimleştirilmiş müşteri kaydına e-Fatura düzenlenemez; UBL zorunlu tanımlayıcı gereksinimleri V14-INV-002
  kapsamındadır.

## Out of scope

- Müşteri hesap bakiyeleri ve anonimleştirmenin yürütülmesi.

## Dependencies

- GATE-V14-ENTRY
- V0-CMP-003

## Deliverables

- `src/Modules/CustomerData/Profiles/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Yetkisiz roller korumalı alanları okuyamaz; gerekli invoice kimliği geçerli kalır; isteğe bağlı PII geçersiz
  kılınabilir/küçültülebilir.
- Her PII alanı `V0-CMP-003` envanterindeki purpose, retention, access owner ve disposal sonucu ile bire bir eşleşir;
  envantersiz alan migration'a giremez.
- `dotnet test tests/Modules/CustomerData/Profiles/ALKAROS.CustomerData.Profiles.Tests.csproj`
  (gerçek Postgres'e karşı, `ALKAROS_TEST_PG_PORT=55432`): 25/25 geçti — Cashier/Manager tam
  görür, diğer her rol tam redaksiyon görür (kısmi sızıntı yok, ama kayıt var olduğu bilgisi
  sızmıyor); alanlar veritabanında düz metin olarak asla görünmüyor; `UpdateContactAsync`/
  `AnonymizeAsync` optimistic concurrency'yi doğru uyguluyor (`CustomerProfileConcurrencyException`);
  anonimleştirme idempotent; anonimleştirilmiş bir kayda `UpdateContactAsync` reddediliyor
  (`CustomerProfileAnonymizedException`); master key değişince okuma güvenle başarısız oluyor
  (`SensitiveDataEncryptionException`, `fail-closed`).
- `dotnet build src/Modules/CustomerData/ALKAROS.CustomerData.csproj` ve
  `dotnet build src/Host/ALKAROS.Host.csproj`: 0 hata.
- `dotnet test tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj`: 9/9 geçti
  (yeni modül `ModuleAssemblies`'e eklendi, `DependsOn` boş olduğu için `ApprovedEdges`'e yeni
  bir satır gerekmedi).
- `dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj` (gerçek Postgres'e
  karşı, `ALKAROS_TEST_PG_PORT=55432`): tam paket ilk geçişte 159/161 (2 gerçek, beklenen
  bulgu — `HostModuleReachabilityTests`/`ManifestTests`'in kendi sabit sayıları, dosyaların
  kendi yorumlarının zaten öngördüğü "her yeni modül/migration bu sayıyı günceller" deseni).
  İkisi de düzeltildi, hedefli yeniden çalıştırma 2/2 geçti; sistemin bellek baskısı altında
  olması nedeniyle tam 161 testlik paket ikinci kez baştan çalıştırılmadı — düzeltilen iki test
  dışındaki 159 testin bu değişiklikle hiçbir ilişkisi yok (aynı dosyalar, aynı assertion'lar,
  değişmedi).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz (yeni modül `MODULE_SCHEMA`'ya
  eklendi; `ICustomerProfileStore`/`PostgresCustomerProfileStore` bilinçli olarak
  `unreachable_services_allowlist.json`'a eklendi — bu görev hiçbir HTTP endpoint eklemiyor).
- `python tools/project-manifest/project_manifest_tool.py` → `VALID (0 differences across
  Solution, Disk, and ProjectReferences)`.

## Handoff

- V14-CST-002
- V14-ACC-001
- V14-INV-002

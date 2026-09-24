# V13-PAY-003 - Compose tender handlers

- Task ID: V13-PAY-003
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: integration
- Surface state: Existing

## Source basis

- PDF:I.26-I.29
- PDF:II.2.6
- PDF:II.5.3
- PDF:III.8

## Goal

Cash handler, durable BankCard workflow ve MealCard provider-registry bridge'ini tek fail-closed registry'de kaydetmek.

## Owned surface

- `src/Modules/Payments/TenderComposition/**`, `tests/Modules/Payments/TenderComposition/**`
- Bu görev handler business logic'i veya provider adapter kodu yazamaz.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/TenderRouting/TenderRequest.cs
  (V13-PAY-002 sahipliğinde kalır — BillId/CashSessionId/IdempotencyKey/RecordedBy opsiyonel
  alanları eklendi, imza geriye dönük uyumlu), src/Modules/Payments/ALKAROS.Payments.csproj
  (TenderComposition/** için Compile Remove — döngüsel proje referansını önlemek amacıyla,
  ALKAROS.Billing.PaymentClosure emsali), ALKAROS.slnx (yeni src ve test projesi kaydı),
  src/Host/ALKAROS.Host.csproj (yeni ALKAROS.Payments.TenderComposition ProjectReference'ı),
  src/Host/Composition/Modules/ModuleRegistry.cs (TenderCompositionModule kaydı eklendi),
  tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs (ApprovedEdges'e
  `Payments.TenderComposition` satırı eklendi), tests/Host/MigrationComposition/Composition/
  HostModuleReachabilityTests.cs (sabit modül sayısı 27→28), tools/plan-audit/plan_audit_tool.py
  (V13-GOV-008 sahipliğinde kalır — bu görev sırasında V13-GOV-008'in
  DONE_DEPENDENCY_TRANSITIVE_NOT_FINAL kontrolündeki gerçek bir hatayı (waiver'ı yalnız
  doğrudan kenarlarda tanıyıp dolaylı zincirlerde tanımaması) bulup düzeltti, bkz. Acceptance
  evidence — bu satır, bağımsız Faz 2 denetiminin bulduğu bir retroaktif Owned surface eksikliğini
  kapatıyor, `V13-GOV-009`) — hepsi paylaşılan dosyalar, plain text (backtick'siz).

## In scope

- Composition registration, duplicate method rejection, disabled capability ve CustomerAccount version routing.

## Out of scope

- Tender işlemi, allocation persistence, provider transport ve CustomerAccount posting.

## Dependencies

- V13-PAY-002
- V13-CSH-003
- V13-PAY-004
- V13-MCD-004

## Deliverables

- `src/Modules/Payments/TenderComposition/**` altında registry composition production code'u.
- Missing, duplicate, disabled ve unsupported method contract testleri.

## Acceptance evidence

**Gate note:** bu görevin `## Dependencies`'inde listelenen `V13-MCD-004`
gerçek kanıtla `Done` olmadan bu görev `Done` işaretlendi — `V13-GOV-008`
(2026-09-23, Semih onaylı) bu spesifik kenarı resmen waive etti
(`plan/GATES.md`'nin `V13_PAYMENT_ORCHESTRATION_DEPENDENCY_WAIVER`
tablosu, `plan_audit_tool.py`/`task_scope_tool.py`'nin eşleşen sabitleri).
MealCard'ın kendisine hiç dokunulmadı — registry'ye hiç girmiyor (aşağıya
bakınız). `V13-MCD-004` ileride tarihli bir kararla `NotApplicable` olsa
bile bu görevin davranışı DEĞİŞMEZ: MealCard bugün de, o zaman da registry'ye
hiç girmiyor; Cash ve BankCard handler registration doğrulaması bağımsız
devam eder.

**BankCard'ın registry'de ele alınışı — Semih'in doğrudan onayladığı
karar (AskUserQuestion, 2026-09-23):** `V13-HUG-001` (gerçek Token/Beko
terminal entegrasyonu) hâlâ Planned ve dış sözleşme kanıtı olmadan
dokunulması yasak (14 waived görevden biri). Bu görevin kendi Acceptance
evidence'ı Cash VE BankCard'ın İKİSİNİN de startup'ta çözülmesini şart
koşuyor — Semih, MealCard'ın aksine BankCard için gerçek bir placeholder
`ITenderHandler` kaydedilmesini seçti (seçenek: "BankCard için de sahte/
no-op bir ITenderHandler kaydet"). `PendingBankCardTerminalIntegrationHandler`
her istekte YALNIZ `TenderRequiresReconciliation` döner — asla sahte bir
Approved/Declined üretmez (CORR:C29 "never guess"); V13-HUG-001 gerçek
terminal kanıtıyla geldiğinde bu sınıf ve kaydı SİLİNECEK, "bitmiş
BankCard desteği" ile karıştırılmamalı (kendi doc comment'inde açıkça
yazılı). Bu, V13-HUG-001'in kendi Owned surface'ına (`src/Modules/
Payments/Token/PaymentRequest/**` vb.) hiç dokunmuyor, ne schema ne de
gerçek adapter kodu üretiyor.

- **Composition registration**: `TenderHandlerRegistryFactory.Build`
  yalnız Cash (`CashTenderMethodAdapter`) ve BankCard
  (`PendingBankCardTerminalIntegrationHandler`) kaydeder; `TenderCompositionModule`
  bunu `ITenderHandlerRegistry` singleton factory'si olarak DI'a bağlar.
- **Duplicate method rejection**: `TenderHandlerRegistry.Register`'ın
  zaten var olan (V13-PAY-002) duplicate-throw davranışına dayanır —
  `FactoryFailsClosedOnADuplicateCashRegistration` testiyle doğrudan
  kanıtlanır.
- **Cash handler bridge**: `CashTenderMethodAdapter`, `ICashTenderHandler`'a
  (V13-CSH-003, dokunulmadı) saf yapısal delegasyon yapar — hiçbir yeni
  business logic yok. `TenderRequest.PaymentId`, Cash için gerçek Payment
  kimliği DEĞİL (CashTenderHandler kendi `Guid.NewGuid()`'ini basıyor) —
  yalnız non-empty bir korelasyon token'ı; gerçek kimlikler yalnız
  `ICashTenderHandler`'ı doğrudan çağırarak veya bu görevin test kanıtındaki
  gibi allocation/ledger/payment tablolarını sorgulayarak görülebilir.
  Bu görevin `CashBridgeThroughTheGenericRouterProducesTheSameRealEffect
  AsTheDirectHandler` testi, generic router üzerinden Cash tender etmenin
  aynı gerçek Payment+PaymentAllocation+CashTransaction etkisini ürettiğini
  gerçek Postgres'e karşı kanıtlar.
- **Disabled capability (MealCard)**: registry'ye hiç kayıt yapılmıyor;
  router zaten var olan `TenderMethodNotRegistered`'ı döner — yeni kod
  gerekmedi (`RegistryLeavesMealCardGenuinelyUnregistered`,
  `RouterReturnsTenderMethodNotRegisteredForMealCard`).
- **CustomerAccount version routing**: değişmedi, regresyon testiyle
  doğrulandı (`RouterReturnsTenderVersionNotEnabledForCustomerAccountUnchanged`).
- **BankCard'ın güvenlik özelliği**: `RouterAlwaysReturnsRequiresReconciliation
  ForBankCardNeverAFabricatedOutcome` beş farklı tutarda BankCard isteğinin
  HER ZAMAN `TenderRequiresReconciliation` döndüğünü, asla Approved/Declined
  olmadığını doğrudan kanıtlar.

**Judgment call — `TenderRequest` genişletmesi:** generic `TenderRequest`
(`PaymentId`, `Method`, `Amount`) Cash'in ihtiyaç duyduğu alanları
taşımıyordu (`CashTenderContracts.cs`'nin kendi doc comment'i bunu bu
görevin işi olarak zaten işaret ediyordu). Dört opsiyonel alan eklendi
(`BillId`/`CashSessionId`/`IdempotencyKey`/`RecordedBy`, hepsi sondaki
optional parametreler — mevcut pozisyonel çağrılar kırılmadı). `TenderHandlerResult`
kayıtlarına (V13-PAY-004'ün zaten tükettiği) hiç dokunulmadı — blast
radius'u sınırlı tutmak için.

**Mimari kısıt — döngüsel proje referansı:** `ICashTenderHandler`'a
referans veren kod `ALKAROS.Payments.csproj` içinde derlenemezdi (Cash zaten
Payments'a referans veriyor — ters yön döngü olurdu). `ALKAROS.Billing.
PaymentClosure.csproj`'un (V13-ALC-002) aynı probleme aynı çözümü birebir
uygulandı: `src/Modules/Payments/TenderComposition/` kendi ayrı projesine
(`ALKAROS.Payments.TenderComposition.csproj`) sahip, `ALKAROS.Payments.csproj`
bu klasörü `<Compile Remove>` ile kendi derlemesinden hariç tutuyor.

**Gerçek doğrulama (2026-09-23, Docker `alkaros-test-pg`, port 55432):**
- `dotnet build ALKAROS.slnx -c Debug` → 0 Uyarı, 0 Hata.
- `dotnet test tests/Modules/Payments/TenderComposition/ALKAROS.Payments.TenderComposition.Tests.csproj` → 8/8 başarılı.
- Regresyon kontrolü: `ALKAROS.Cash.TenderHandler.Tests` 6/6, `ALKAROS.Payments.TenderRouting.Tests` 28/28, `ALKAROS.Payments.CardSettlement.Tests` 8/8, `ALKAROS.Architecture.Tests` (ModuleBoundaries) 9/9 — hepsi yeşil, sıfır regresyon.
- `dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj` → 161/161 (yeni `TenderCompositionModule`'ün DI kompozisyonu dahil).
- `python tools/project-manifest/project_manifest_tool.py` → `VALID (0 differences)`.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- V13-PUI-001
- V13-PAY-005
- V14-ACC-003
- V14-ACC-008

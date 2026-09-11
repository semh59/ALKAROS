# V1-WTR-012 - Kişisel günlük ikram bütçesi

- Task ID: V1-WTR-012
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`garson-karsilastirma` karşılaştırma dokümanının "Yeni fikirler" bölümünde
(Katman A, madde 1) önerilen ilk özellik. Sorun: `bills.comp` taşımayan bir
rol (yalnız `waiter`) için HER ikram talebi — bir özür çayı bile — bugüne
kadar yöneticiye gidiyordu, tıpkı ₺500'lük bir yazım gibi aynı bekleyen
grant'i açıyordu. Semih'in onayladığı parametreler: günlük ₺150, kalem başı
₺50, yalnız `waiter` rolü, takvim günü (UTC 00:00) sıfırlama.

Düzeltme: var olan `IAuthorizationGrantService` escalation-resolver
zincirinin (`DelegationEscalationResolver`'ın zaten kullandığı aynı
uzantı noktası) üstüne yeni bir `PersonalCompBudgetEscalationResolver`
eklendi — sıfırdan bir bütçe/ledger tablosu gerekmedi: her kendi-kendine
onaylanan ikram zaten `identity.authorization_grants`'e normal bir
`granted` satırı olarak yazılıyor (`policy_path = 'personal_budget'`),
günlük harcanan tutar bu tablodan `SUM` ile hesaplanıyor. Bu tasarımın
sağladığı, ayrı bir tablonun sağlayamayacağı üç şey:

1. **Own-check guard bedava.** Bu resolver, own-check guard'ın (bir garson
   yalnız kendi servis ettiği hesabı ikram edebilir) zaten geçtiği bir
   noktada çalışıyor — own-check'i yeniden yazmak/atlamak riski yok.
2. **Behavioral tightening bedava.** `BehaviouralTighteningGate` zorla
   escalation yaptırdığında, bu resolver de (mevcut Delegation resolver
   gibi) hâlâ çalışıyor — bilinçli, var olan tasarımla tutarlı (bkz.
   `AuthorizationGrantService.RequestAsync`'in kendi akışı).
3. **Audit izi bedava.** Her kendi-kendine onaylanan ikram, `reason_code`,
   `requester_user_id`, `amount`, `resolved_at` taşıyan gerçek bir grant
   satırı — `reporting.authorization_grant_daily` view'ı bunu otomatik
   raporluyor.

Uç nokta (`POST .../comp`) artık `ApplyComplimentaryResultV1`'e
`PersonalBudgetRemaining` (yalnız bu yol devreye girdiğinde dolu) ekliyor;
`waiter-app.js`'nin `confirmComp()`'u bunu okuyup "Bugünkü ikram
hakkınızdan ₺X kaldı" toast'ını gösteriyor — sınırın ne zaman bittiğini
garson göremeden bütçe kullanışsız kalırdı.

**Bulunan ve düzeltilen bir kendi hata:** resolver'ı ilk
`services.TryAddSingleton<IEscalationResolver, ...>()` ile kaydettim —
`TryAdd*`'ın yalnız SERVİS tipine baktığını (implementasyona değil)
unutmuşum; `DelegationEscalationResolver` zaten `IEscalationResolver`'ı
kaydettiği için ikinci `TryAddSingleton` sessizce hiçbir şey yapmadı,
resolver'ım hiç çalışmadı. Yeni testler tam bunu yakaladı (Accepted
bekleniyordu, testler `OK` bekliyordu ama `Accepted` döndü) — gerçek,
kazara olmayan bir kanıt: testler `TryAddSingleton` → `AddSingleton`
düzeltmesinden ÖNCE gerçekten kırmızıydı, düzeltmeden SONRA yeşile döndü.

## Owned surface

- `plan/v1/waiter-pwa/V1-WTR-012-personal-comp-budget.md` (yeni)
- `database/migrations/V1/V1-WTR-012/**` (yeni)
- `src/Modules/Identity/Authorization/PersonalBudgets/**` (yeni)
- Sınırlı ek:
  - src/Modules/Identity/Authorization/Grants/GrantStatus.cs (Identity
    sahipliğinde) — yeni `PolicyPath.PersonalBudget` + metin eşlemesi.
  - src/Modules/Identity/Authorization/Grants/IAuthorizationGrantRepository.cs,
    PostgresAuthorizationGrantRepository.cs (Identity sahipliğinde) — yeni
    `SumPersonalBudgetGrantedSinceAsync`.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs,
    OrderManagementContracts.cs (Host sahipliğinde) — resolver DI kaydı,
    `/comp` uç noktasının `PersonalBudgetRemaining` hesaplaması.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-010 sahipliğinde)
    — `confirmComp()`'un kalan bütçe toast'ı.
  - database/MigrationComposition/order.json — 099 kaydı.
  - src/Host/Composition/Migrations/MigrationManifest.cs — PhaseBMax.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs — manifest
    testinin sınır değerleri (099 dahil; sınır-dışı testi artık
    `PhaseBMax`'tan türetiliyor, sabit bir sayı değil).
  - tests/Host/Experience/Orders/Comp/OrderManagementCompHttpTests.cs,
    OrderManagementCompTestDatabase.cs,
    ALKAROS.Host.Experience.Orders.Comp.Tests.csproj (Host test
    sahipliğinde) — yeni testler, fiyat parametreli seed overload'ı, yeni
    migrasyonun test fixture'ına eklenmesi.

## Out of scope

Diğer "Katman A" fikirleri (vardiya devri notu, yardım çağır sinyali,
taslakta fiyat/stok değişti işareti) — ayrı görevler, sırayla. Bütçenin
gerçek zamanlı ayarlar ekranından değiştirilebilmesi — şimdilik sabit
(kod içi) politika, bkz. `PersonalCompBudgetPolicy`'nin kendi doc yorumu.

## Dependencies

- V1-RMD-177
- V1-IAM-019
- V1-IAM-020
- V1-IAM-021
- V1-IAM-023

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` (tüm çözüm) → 0 uyarı, 0 hata.
- `node --check waiter-app.js` → temiz.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test`:
  - `tests/Host/Experience/Orders/Comp/*.csproj` → 13/13 yeşil (4 yeni
    test: bütçe dahilinde doğrudan uygulanır + kalan tutar doğru; bills.comp
    taşıyan rol hiç `PersonalBudgetRemaining` raporlamaz; kalem başı
    tavanın üstü hâlâ yöneticiye gider; üç ₺50 ikramdan sonra dördüncü
    günlük tavanı aşınca hâlâ yöneticiye gider — sınır tam ₺150'de dahil).
    **Vacuous olmadığının kanıtı organik**: `TryAddSingleton` hatası
    düzeltilmeden önce bu 4 testten 2'si gerçekten kırmızıydı (`Accepted`
    bekleniyordu `OK` yerine) — düzeltmeden sonra yeşile döndü.
  - `tests/Host/Experience/Orders/TableDraft/*.csproj` → 51/51 yeşil
    (regresyon yok).
  - `tests/Host/Experience/Orders/VoidSent/*.csproj` → 14/14 yeşil
    (regresyon yok).
  - `tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj`
    → 9/9 yeşil.
  - `tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj --filter
    "FullyQualifiedName~ManifestTests"` → 16/16 yeşil (biri, sınır-dışı
    değeri artık `PhaseBMax`'tan türeten düzeltmeyle birlikte).
  - `tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj --filter
    "FullyQualifiedName~DualScreenAuthorizationHttpTests"` → 5/5 yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal (migrasyon dosyası İngilizce yeniden
  yazıldı, C# yorumlarındaki ₺ sembolü kaldırıldı — kod/yorum İngilizce
  kuralına uyuldu). Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.

## Handoff

- None

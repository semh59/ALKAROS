# V1-RMD-247 - Wire RecipeCostSnapshotService into a real management endpoint

- Task ID: V1-RMD-247
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`IRecipeCostSnapshotService` (`V11-RCP-002`, waste-factor + moving-average
recipe costing) domain-complete ve modül testleriyle kapsanmış olduğundan
beri hiçbir HTTP endpoint'inden çağrılmıyordu. Bir bağımsız denetim ajanı
(2026-09-18, tüm proje kod denetimi, Catalog/Menu/Recipes/Production/
Inventory/Purchasing alanı) bunu tespit etti. Aynı denetim ayrıca
`RecipeCostSnapshot.CalculatedCost`'un porsiyon başına değil, tüm reçete
partisinin toplam maliyeti olduğunu, `RecipeVersion.YieldQuantity`'ye hiç
bölünmediğini not etti — şu ana kadar hiç kullanılmadığı için "aktif" bir
hata değildi, ama bu görev onu gerçekten kullanıcıya gösteren ilk yüzey
olduğu için burada ele alınıyor (domain sınıfının kendisi DEĞİL, yalnız
Host DTO'sunda ek bir alan).

## Owned surface

- `evidence/V1-RMD-247/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Recipes/RecipeCostSnapshotEndpoints.cs
  (yeni dosya, dizin V11-RCP-003 sahipliğinde kalır) — yalnız bu yeni
  dosya eklenir, V11-RCP-003'ün kendi `RecipeCatalogMappingEndpoints.cs`
  dosyasına dokunulmaz.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Recipes/RecipeCostSnapshot*.cs
  (yeni dosyalar, dizin V11-RCP-003 sahipliğinde kalır — mevcut
  `ALKAROS.Host.Experience.Recipes.Tests.csproj`'a eklenir, yeni bir proje
  gerekmez) — V11-RCP-003'ün kendi test dosyalarına dokunulmaz.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs
  (çok sayıda geçmiş dalga görevinin sahipliğinde kalır) — servis kaydı ve
  route eşleme zincirine ekleme.

## In scope

- `POST /api/v1/management/recipes/{recipeVersionId}/cost-snapshots` —
  `{CostBasisDate, Currency?}`, gerçek `CreateSnapshotAsync` üzerinden.
- `GET /api/v1/management/recipes/{recipeVersionId}/cost-snapshots/effective?asOfDate=...` —
  `GetEffectiveSnapshotAsync`; bulunamazsa 404.
- Response DTO'suna, `RecipeVersion.YieldQuantity`'ye bölünmüş
  `CostPerPortion` alanı eklenir (domain sınıfı değişmez, yalnız Host'un
  kendi projeksiyonu) — porsiyon başına maliyeti ilk kez gerçekten
  gösteren yer burası, `YieldQuantity`'ye bölünmeme riski ilk kullanımda
  kapatılmış olur.
- `inventory.manage` izni (RecipeCatalogMappingEndpoints'in zaten
  kullandığı, aynı "reçete/envanter verisi" ailesi).

## Out of scope

- `RecipeCostSnapshot`/`RecipeCostSnapshotItem` domain sınıflarının kendi
  `CalculatedCost`/`AddItem` hesaplama mantığı — değişmez.
- `StockItemUnits`/`FallbackItemCosts` gibi ileri parametreler için tam bir
  UI — yalnız temel oluşturma/okuma akışı.

## Dependencies

- V11-RCP-002

## Acceptance evidence

- Gerçek Postgres + gerçek Host'a karşı HTTP testi: gerçek bir reçete
  sürümü + gerçek satın alma geçmişi (moving-average maliyet) ile bir
  snapshot oluşturulup `CalculatedCost` VE `CostPerPortion` (= CalculatedCost
  / YieldQuantity) doğru döner; maliyet verisi olmayan bir malzeme 4xx
  döner (`MissingCostBasisException`); aynı (versionId, date) ile ikinci
  oluşturma 409 döner; `inventory.manage` izni olmayan çağrı 401/403 alır.
- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test` (yeni testler) → yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None

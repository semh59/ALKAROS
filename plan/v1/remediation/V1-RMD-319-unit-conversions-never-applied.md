# V1-RMD-319 - Özel birim dönüşümleri hiçbir zaman çalışma zamanına uygulanmıyordu

- Task ID: V1-RMD-319
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) K7 bulgusu: `recipe.unit_conversions`'a bir yönetici tarafından eklenen özel dönüşümler (örn. "1 koli = 12 adet") gerçekten kalıcı, listelenebilir kayıtlardı, ama hiçbiri gerçek bir hesaplamayı etkilemiyordu. Üç ayrı köke iniyordu: (1) `IUnitConverter` arayüzü `RegisterConversion`'ı hiç dışa açmıyordu — kod tabanındaki HER çağıran (kural olarak somut `UnitConverter` değil arayüz enjekte ediliyor) bunu hiçbir şekilde çağıramazdı; (2) altı ayrı `Host/Experience/*` dosyasında ve iki modülde tutarsız DI ömrü (bazıları `Transient`, bazıları `Singleton`) vardı — `Transient` kazanan her yerde her çözümleme taze, yalnız `StandardUnits` yüklü bir örnek alıyordu; (3) gerçekten hiçbir yerden `RegisterConversion` çağrılmıyordu (repo-genelinde grep sıfır sonuç).

## Owned surface

- `plan/v1/remediation/V1-RMD-319-unit-conversions-never-applied.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/BuildingBlocks/Measurements/IUnitConverter.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/BuildingBlocks/Measurements/UnitConverter.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Recipes/RecipesModule.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Inventory/InventoryModule.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Inventory/StockMasterEndpoints.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Recipes/RecipeCatalogMappingEndpoints.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Recipes/RecipeManagementEndpoints.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs
  (yalnız yeni hosted service'in `AddHostedService` çağrısı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Recipes/UnitConversionLoaderHostedService.cs
  (yeni dosya, klasör V11-RCP-003 tarafından zaten sahiplenilmiş)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/BuildingBlocks/Measurements/DimensionSafeUnitTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Recipes/RecipeManagementHttpTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Recipes/RecipeCatalogMappingTestDatabase.cs

## In scope

1. `IUnitConverter`'a `RegisterConversion` eklenir (arayüz üzerinden çağrılabilir hâle gelir).
2. Her yerdeki DI ömrü `Singleton`'a hizalanır (iki modül + dört Host Experience dosyası).
3. `UnitConversionLoaderHostedService` (yeni): açılışta `recipe.unit_conversions`'daki aktif satırları paylaşılan singleton'a yükler; bir DB hatası tüm Host'un başlamasını engellemez (yalnız bu özelliği yüklemeden devam eder), çelişkili tek bir satır diğerlerinin yüklenmesini bloklamaz.
4. `RecipeManagementEndpoints.cs`'in `POST /unit-conversions`'ı: kalıcı kayıttan ÖNCE çalışma zamanı dönüştürücüsüne uygulanır (çelişkili bir çift kalıcı yazılmadan reddedilir), böylece yeniden başlatma gerekmeden anında etkili olur.
5. `UnitConverter.RegisterConversion`'ın kendi çelişki tespiti: aynı çiftin GERÇEKTEN farklı bir faktörle güncellenmesi (mevcut "aynı çifti tekrar postalamak faktörü değiştirir" davranışını bozmadan) artık çelişki olarak reddedilmiyor; yalnız ters yöndeki gerçek çelişkiler hâlâ reddediliyor.

## Out of scope

- `RegisterUnit`'in arayüze eklenmesi — bu görevin bulgusu yalnız `RegisterConversion`'ı kapsıyor.
- `IUnitConverter`'ın iki modül tarafından bağımsız kaydedilmesinin (aynı somut tipe) tekilleştirilmesi — davranışsal olarak zararsız (ikisi de aynı `UnitConverter` somut tipini gösteriyor), kapsam dışı bırakıldı.
- Production modülünün kendi birim dönüşüm mantığını merkezi `IUnitConverter`'ı hiç kullanmadan ayrı yazmış olması (`ProductionStockEffectService.cs:530-575`) — orta seviye bulgu, ayrı görev.

## Dependencies

- None

## Acceptance evidence

Host testleri (UTF8 Postgres 18), Measurements birim testleri, gerçek bir HTTP sunucusuna karşı:

- `ALKAROS.Measurements.Tests`: 13/13 (1 yeni: aynı çiftin farklı bir faktörle güncellenmesi artık çelişki fırlatmıyor).
- `ALKAROS.Host.Experience.Recipes.Tests`: 16/16 (2 yeni) — `AddingACustomConversionMakesItImmediatelyUsableByTheSharedRuntimeConverter`: POST sonrası AYNI çalışan uygulamanın paylaşılan `IUnitConverter`'ı (iki farklı `GetRequiredService` çağrısı) gerçekten dönüştürüyor; `AConversionAlreadyPersistedBeforeStartupIsUsableFromTheFirstRequest`: HTTP çağrısı olmadan, doğrudan DB'ye seed edilmiş bir satır, gerçek bir `UnitConversionLoaderHostedService` içeren AYRI bir uygulama örneğinde başlangıçtan itibaren kullanılabiliyor.
- `ALKAROS.Host.Experience.Composition.Tests`: 10/10 — bu paket, Postgres'e kasıtlı olarak bağlanamayan sahte bir `NpgsqlDataSource` kullanıyor; ilk uygulama denemem `UnitConversionLoaderHostedService.StartAsync`'in DB hatasını yutmadığı için TÜM paketi kırdı (8/10 başarısız) — bu, "geçici bir DB kesintisi hiçbir zaman Host'un başlamasını bloklamamalı" ilkesinin gerçek bir ihlaliydi, ayrı bir try/catch ile düzeltildi (bu bulgunun kendi kapanışının BİR PARÇASI olarak, kanıt bölümüne dürüstçe kaydediliyor).
- `ALKAROS.Host.Experience.Inventory.Tests` 20/20, `ALKAROS.Host.Experience.Purchasing.Tests` 3/3, `ALKAROS.Host.Experience.NfcOrdering.Tests` 19/19 — regresyon yok.

Mutasyon kontrolü (üç ayrı fix için ayrı ayrı): (1) `RecipeManagementEndpoints.cs`'in canlı `RegisterConversion` çağrısı kaldırıldı — yeni test kırmızı oldu; (2) `UnitConversionLoaderHostedService.StartAsync`'in gövdesi devre dışı bırakıldı — yeni test kırmızı oldu; (3) `UnitConverter`'ın "doğrudan kayıtlı çift" kontrolü kaldırıldı — yeni test kırmızı oldu (`ContradictoryUnitConversionException` fırlattı). Üçünde de dosyalar `diff` ile birebir orijinaline geri getirildi, tüm paketler tekrar yeşil.

## Handoff

- None

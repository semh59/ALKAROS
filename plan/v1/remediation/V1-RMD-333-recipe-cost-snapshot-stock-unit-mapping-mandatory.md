# V1-RMD-333 - Reçete maliyet anlık görüntüsü artık stok birimini asla sessizce tahmin etmiyor

- Task ID: V1-RMD-333
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) orta seviye bulgusu: "Reçete maliyet anlık görüntüsü stok birimini
istemcinin isteğe bağlı sözlüğünden alıyor; unutulursa maliyet ~1000 kat yanlış hesaplanabilir, hatasız"
(`IRecipeCostSnapshotService.cs:44-108`). Doğrulandı: `RecipeCostSnapshotService.CreateSnapshotAsync`, bir
malzemenin GERÇEK stok takip birimini (`inventory.stock_items.tracking_unit_code`) hiç okumuyordu —
`RecipeCostSnapshotEndpoints.cs`'nin kendi belge yorumu bunun kasıtlı olduğunu doğruluyor: bu servis, onaylanmış
mimari sınırı (V0-ARC-001) korumak için Inventory'ye doğrudan bağımlı DEĞİL. Bunun yerine `CreateSnapshotCommand`,
çağırandan (`StockItemUnits`) isteğe bağlı bir birim eşlemesi bekliyordu; eksikse kod sessizce reçetenin kendi
native birimini (`ingredient.UnitCode`) stok birimiymiş gibi kullanıyordu. Bu iki birim aynı olmadığında (örn.
reçete "kg" ile yazılmış, stok "g" ile takip ediliyor), dönüşüm hiçbir hata vermeden kuruş-kesin bir sonuç üretiyor
— sadece 1000 kat yanlış.

Bu boşluk hiçbir mevcut testte görünmedi çünkü TÜM test fixture'ları (hem birim hem HTTP testleri) tesadüfen
native birim ile gerçek stok birimini aynı seçmişti (`kg`==`kg`).

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Recipes/CostSnapshots/IRecipeCostSnapshotService.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Recipes/CostSnapshots/Exceptions.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Recipes/RecipeCostSnapshotEndpoints.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Recipes/CostSnapshots/RecipeCostSnapshotDatabaseTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Recipes/RecipeCostSnapshotHttpTests.cs
- `plan/v1/remediation/V1-RMD-333-recipe-cost-snapshot-stock-unit-mapping-mandatory.md`

## In scope

1. `CreateSnapshotAsync`: `command.StockItemUnits`'te bir malzemenin stok kalemi için giriş yoksa artık sessizce
   `ingredient.UnitCode`'a düşmüyor — yeni `MissingStockUnitMappingException` fırlatıyor.
2. Yeni istisna `RecipeCostSnapshotEndpoints.cs`'nin hata haritasına `400 MISSING_STOCK_UNIT_MAPPING` olarak
   eklendi (temel `RecipeCostSnapshotException`'ı yakalayan genel `catch` zaten vardı, ama switch'te eşleşmeyen bir
   alt tip `_ => throw exception` dalına düşüp 500 olarak sızabilirdi — Türkçe/yapısal hata sözleşmesi için
   açıkça eklendi).
3. Mevcut TÜM test çağrı yerleri (hem `RecipeCostSnapshotDatabaseTests.cs` hem `RecipeCostSnapshotHttpTests.cs`)
   gerçek stok biriminin ("kg") açıkça belirtildiği şekilde güncellendi; ayrıca eksik eşleme senaryosunun kendi
   yeni, ayrı testleri eklendi.

## Out of scope

1. Servisin kendisinin Inventory'den doğrudan okuma yapması (`inventory.stock_items.tracking_unit_code`'u
   otomatik çözmesi) — `RecipeCostSnapshotEndpoints.cs`'nin kendi belge yorumunun açıkça koruduğu onaylı mimari
   sınırı (V0-ARC-001, "no Inventory") ihlal eder. Bu, ayrı bir mimari karar (Semih'in onayı) gerektirir.
2. Bu servise gerçek bir istemci/arayüz eklemek — `RecipeCostSnapshotEndpoints.cs` zaten "beş sıfır arayüzlü arka
   uç yüzeyi"nden biri (bkz. bu oturumun `pending-frontend-for-dead-module-wiring` notu), bu görevin kapsamı
   yalnızca servisin kendi sessiz hata riskini kapatmak.

## Dependencies

- None

## Acceptance evidence

- `tests/Modules/Recipes/CostSnapshots/ALKAROS.Recipes.CostSnapshots.Tests.csproj`: 13/13 test geçti (1 yeni test
  dahil: `OmittingTheStockUnitMappingRefusesInsteadOfSilentlyAssumingTheNativeUnitIsCorrect`).
- `tests/Host/Experience/Recipes/ALKAROS.Host.Experience.Recipes.Tests.csproj`: 17/17 test geçti (1 yeni test
  dahil: `OmittingTheStockUnitMappingIsRejectedWithItsOwnErrorCode`, gerçek `MISSING_STOCK_UNIT_MAPPING` hata
  kodunu doğruluyor).
- Mutasyon kontrolü: `stockUnit` çözümü geçici olarak eski sessiz-varsayılan haline döndürüldü (`?? ingredient.UnitCode`),
  yeni test GERÇEK bir çalışma zamanı davranış farkıyla kırmızıya döndü — `Expected
  MissingStockUnitMappingException, but found MissingCostBasisException` (yanlış birimle "g" olarak hesaplanan
  stok miktarı, kg fiyatıyla çarpılınca maliyet tabanı bulunamadı hatasına düştü, denetimin tarif ettiği sessiz
  1000x hata riskini birebir doğruluyor). Düzeltme geri getirildi, her iki paket yeniden 13/13 ve 17/17 yeşile
  döndü.
- `ALKAROS.Recipes.csproj` ve `ALKAROS.Host.csproj`: sıfır hata, sıfır uyarı ile derlendi.

## Handoff

- None

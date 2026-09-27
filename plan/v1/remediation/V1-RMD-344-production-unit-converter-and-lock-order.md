# V1-RMD-344 - Production modülü artık merkezi IUnitConverter'ı kullanıyor ve stok kilitlerini global sırayla alıyor

- Task ID: V1-RMD-344
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) orta seviye bulgusu: "Production modülü kendi birim dönüşüm
mantığını ayrı yazmış, merkezi `IUnitConverter`'ı hiç kullanmıyor (`ProductionStockEffectService.cs:530-575`);
stok kilitleme sırası global değil (deadlock riski)." Doğrulandı, iki ayrı gerçek kusur:

1. `ResolveConversionFactorAsync`: metrik kütle/hacim çiftlerini (g/kg/mg, ml/l/cl) elle kodlanmış bir liste
   olarak taşıyordu, başka her şey için `recipe.unit_conversions` tablosuna ham SQL sorgusu atıyordu. Bu, bu
   oturumun aynı dosyada daha önce düzelttiği (V1-RMD-319, K7) merkezi `IUnitConverter`'ın TAM OLARAK aynı
   yerleşik tabloyu taşıdığı bir yinelemeydi — ayrıca `IUnitConverter`'ın Sayı boyutu (adet/piece/portion/pack/
   box/porsiyon/paket/koli) birimlerini hiç bilmiyordu, bu yüzden metrik olmayan iki farklı birim arasında
   (örn. "adet" ↔ "piece") DB'de özel bir satır olmadan asla dönüşüm yapamıyordu.
2. Stok bakiyesi satırlarını kilitleyen `FOR UPDATE` döngüsü, malzemeleri reçetenin kendi `sort_order`'ına göre
   sırasız kilitliyordu — bu oturumun `OrderStockConsumptionService.LockStockRowsAsync`'de (V12-RMD-003) aynı
   sınıf risk için zaten kurduğu kuralı ("her zaman `stock_item_id`'ye göre global sıralı kilitle") ihlal
   ediyordu. Örtüşen malzemeleri farklı `sort_order` dizileriyle listeleyen iki reçete, aynı anda çalışan iki
   üretim partisini birbirine kilitleyebilirdi (deadlock).

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Production/StockEffects/ProductionStockEffectService.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Production/StockEffects/Exceptions.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Production/StockEffects/ProductionStockEffectsDatabaseTests.cs
- `plan/v1/remediation/V1-RMD-344-production-unit-converter-and-lock-order.md`

## In scope

1. `ProductionStockEffectService`'in yapıcısına `IUnitConverter` eklendi (zaten Recipes/Inventory modülleri
   tarafından Singleton olarak kayıtlı — DI değişikliği gerekmedi); `ResolveConversionFactorAsync`'in TAMAMI
   silindi, tek çağrı yeri `_unitConverter.Convert(...)`'e yönlendirildi. `UnknownUnitException`/
   `IncompatibleUnitDimensionException` yakalanıp mevcut `InvalidProductionStockEffectException` sözleşmesine
   çevriliyor (davranış dışarıdan aynı görünüyor, iç uygulama artık paylaşılan).
2. Stok doğrulama döngüsü çalıştırılmadan ÖNCE `plannedConsumptions`, `stock_item_id`'ye göre sıralanıyor
   (`OrderStockConsumptionService.LockStockRowsAsync`'in aynı, zaten kurulmuş global sıralama kuralı).

## Out of scope

1. Hareket kayıtlarını (movements) yazma döngüsünün sırası — kilitleme AŞAMASI dışında sıralamanın hiçbir
   doğruluk etkisi yok, değiştirmeye gerek yok.
2. `IUnitConverter`'ın Sayı boyutu birimlerinin (adet/koli/paket/vb.) hepsinin factor=1.0 ile kayıtlı olması —
   bu, `UnitConverter`'ın kendi mevcut davranışı, bu görevin kapsamının dışında, ayrı bir potansiyel bulgu.

## Dependencies

- None

## Acceptance evidence

- `tests/Modules/Production/StockEffects/ALKAROS.Production.StockEffects.Tests.csproj`: 7/7 test geçti (1 yeni
  test dahil: `ACountDimensionCrossUnitIngredientConvertsThroughTheSharedUnitConverterWithNoCustomDbRow` — "adet"
  → "piece" dönüşümü `recipe.unit_conversions`'da HİÇBİR özel satır olmadan başarıyla tamamlanıyor; eski kod bunu
  asla yapamazdı).
- Mutasyon kontrolü: `_unitConverter.Convert(...)` çağrısı geçici olarak eski hardcoded-metrik-yalnızca mantığa
  döndürüldü, yeni test GERÇEK bir çalışma zamanı hatasıyla kırmızıya döndü
  (`InvalidProductionStockEffectException: No unit conversion factor found between 'adet' and 'piece'`).
  Düzeltme geri getirildi (kalıntı bırakılmadığı `grep` ile doğrulandı), paket yeniden 7/7 yeşile döndü.
- `tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj`: 9/9 test geçti — `IUnitConverter`
  bağımlılığı (bir BuildingBlock, modül değil) hiçbir modül sınırı ihlali yaratmadı.
- `dotnet build src/Modules/Production/ALKAROS.Production.csproj`: sıfır hata, sıfır uyarı.

## Handoff

- None

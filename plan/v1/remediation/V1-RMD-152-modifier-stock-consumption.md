# V1-RMD-152 - Eklentiler gerçek malzeme tüketiyor ama stoktan düşmüyor

- Task ID: V1-RMD-152
- Status: Done
- Assignee: Claude Opus 5
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-143'ün "Out of scope" kaydından beri açık duran ve V1-RMD-150'de
yeniden doğrulanan boşluk: bir kalemin eklentileri hiç stok tüketmiyor.
`OrderStockConsumptionService` yalnız `item.ProductId`'ye bakıyor,
`item.Modifiers`'a hiç bakmıyor. Ekstra peynir gerçek peynirdir; V1-RMD-150
onu doğru adetle ücretlendirip mutfak biletine yazdıktan sonra depodan
düşmemesi, satılan ile sayılan arasında sessiz bir fark bırakıyor. Bugünkü
şemayla eşleme kurmak da mümkün değil: `inventory.product_stock_mappings`
yalnız `catalog.products` satırlarına bağlanabiliyor, `catalog.modifiers` ise
bağımsız bir tablo (`ProductType.Modifier` enum'da tanımlı ama ne kodda ne
veride kullanılıyor).

## Owned surface

- `plan/v1/remediation/V1-RMD-152-modifier-stock-consumption.md` (yeni)
- `database/migrations/V1/V1-RMD-152/**` (yeni) — migration 095.
- `src/Modules/Inventory/ModifierStock/**` (yeni) — eklenti-stok eşlemesinin
  kendi modeli ve deposu.
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - src/Host/Experience/Orders/OrderStockConsumption/OrderStockConsumptionService.cs
    (V1-RMD-143 sahipliğinde) — eklenti tüketimi.
  - src/Host/Experience/Inventory/StockMasterEndpoints.cs,
    src/Host/Experience/Inventory/StockMasterContracts.cs (V1-RMD-143
    sahipliğinde) — eşlemeyi tanımlayan yönetim uç noktaları.
  - src/Modules/Inventory/InventoryModule.cs (V11-INV-00x sahipliğinde) —
    yeni deponun kaydı.
  - database/MigrationComposition/order.json,
    src/Host/Composition/Migrations/MigrationManifest.cs,
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-FND-004
    sahipliğinde) — migration 095 için standart dört dosyalık desen.
  - tests/Host/Experience/Orders/TableDraft/**,
    tests/Host/Experience/Inventory/** (ilgili görevler sahipliğinde).

## In scope

1. **Eklentinin kendi eşlemesi.** `inventory.modifier_stock_mappings`
   (`modifier_id`, `stock_item_id`, `quantity_multiplier`), ürün eşlemesinin
   birebir aynı şekli ve aynı BOM aritmetiği. Ayrı bir tablo, çünkü bir
   eklenti `catalog.products` satırı değil.
2. **Tüketim V1-RMD-150'nin adedini kullanır.** Düşülen miktar
   `modifier.Quantity × mapping.QuantityMultiplier`. İki tabaklık siparişte
   iki porsiyon peynir ücretlendirildiyse iki porsiyon peynir de düşer —
   ücret, mutfak bileti ve depo aynı sayıyı kullanır.
3. **Hareket kalemin kimliğine bağlanır.** `sourceReferenceId` yine
   `item.Id`; böylece V1-RMD-143'ün Accept sonrası iade yolu
   (`SentItemVoidStore`) eklenti hareketlerini de kendiliğinden geri verir,
   ayrı bir iade koduna gerek kalmaz.
4. **Eşlemesi olmayan eklenti siparişi durdurmaz.** Ürün için kural
   "eşleme yoksa reddet"tir (Semih'in V1-RMD-143 kararı) ve öyle kalıyor;
   eklenti için kasıtlı olarak farklı: eklentilerin çoğu malzeme değil bir
   talimattır ("az pişmiş", "soğansız", "Tam porsiyon"). Her ücretsiz
   seçenek için stok kalemi tanımlatmak yapılandırmayı anlamsızca
   şişirirdi. Eşlemesi olan tüketir, olmayan tüketmez — bu bir hata yutma
   değil, kasıtlı ve burada belgelenen bir iş kuralıdır.

## Out of scope

- Eşlemesi olmayan ücretli eklentiyi yöneticiye raporlayan bir uyarı
  yüzeyi: değerli ama ayrı bir iş (aynı şekilde eşlemesiz ürün de bugün
  yalnız sipariş anında fark ediliyor).
- `ProductType.Modifier`'ın hiç kullanılmaması: enum değeri ölü, ama onu
  kaldırmak bu görevin kapsamı dışında bir temizlik.

## Dependencies

- V1-RMD-150

## Acceptance evidence

- `dotnet build ALKAROS.slnx`: 0 Uyarı, 0 Hata.
- Migration 095: boş bir veritabanında gerçekten çalıştırıldı — ileri yönde
  `inventory.modifier_stock_mappings` oluştu, geri yönde kalmadı (tablo
  sayısı 1 → 0 olarak doğrulandı).
- Gerçek Postgres'e karşı (`alkaros-test-pg`, port 55432), ayrı ayrı,
  gerçek çıkış koduyla:
  - `ALKAROS.Host.Experience.Orders.TableDraft.Tests`: **32/32** (29'dan).
    Üç yeni senaryo: eşlemesi olan eklenti iki porsiyonluk gönderimde
    10'dan 8'e düşüyor (V1-RMD-150'nin adedi kadar); eşlemesi olmayan
    eklenti siparişi durdurmuyor; eklentinin stoğu yetmediğinde tüm
    gönderim 409 ile reddediliyor, ürünün kendi bakiyesi de geri alınıyor
    ve mutfak bileti hiç yazılmıyor.
  - `ALKAROS.Host.Experience.Orders.VoidSent.Tests`: **13/13** (12'den).
    Yeni test, aynı kaleme bağlı birden fazla tüketimin iptalde hepsinin
    geri geldiğini doğruluyor — eklenti hareketi de ürününkiyle aynı
    `item.Id`'ye yazıldığı için mevcut iade yolu onu ayrı koda gerek
    kalmadan kapsıyor.
  - `ALKAROS.Host.Experience.Inventory.Tests`: **11/11** (9'dan). Eklenti
    eşlemesi atanıyor, `AvailableQuantity` çarpanla doğru hesaplanıyor
    (30 / 3 = 10), kaldırılıyor; olmayan eşlemenin silinmesi 404.
  - `ALKAROS.Host.Experience.Orders.Confirmation.Tests`: 19/19,
    `...NfcOrdering.Tests`: 17/17, `ALKAROS.Host.Tests`: 134/134.
- Semih'in elle deneyebileceği senaryo: bir stok kalemi ("Peynir") oluştur
  ve "Ekstra peynir" eklentisine eşle, bakiyesini 10 yap; o eklentiyle iki
  porsiyonluk bir sipariş gönder ve bakiyenin 8'e düştüğünü gör; aynı
  kalemi mutfak başlamadan iptal et ve bakiyenin 10'a döndüğünü gör.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var olan
  ihlal (`InventoryAdjustmentService.cs:96`, bu görevden bağımsız), yeni
  ihlal yok. (Bu görevin kendi yorumlarında denetimin yakaladığı iki
  Türkçe karakter sızıntısı İngilizceye çevrilerek giderildi.)

## Handoff

- None

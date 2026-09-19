# V1-RMD-239 - Convert the goods-receipt unit before posting a stock effect

- Task ID: V1-RMD-239
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`PurchasingService.ReceiveGoodsAsync` posted `item.AcceptedQuantity` (in
the purchase-order line's OWN unit, e.g. a supplier's "box") straight to
`IStockBalanceRepository.ApplyOnHandDeltaAsync` and the resulting
`StockMovement`, with no comparison against — or conversion into — the
`StockItem`'s own `TrackingUnitCode` (e.g. "kg"). A bağımsız denetim ajanı
(2026-09-18, tüm proje kod denetimi, Catalog/Menu/Recipes/Production/
Inventory/Purchasing alanı) bunu tespit etti: 5 kutu (24'lü) teslim
alındığında stoğa 5 ekleniyordu, 120 değil — gerçek bir stok doğruluğu
hatası. `InventoryAdjustmentService` (aynı modül grubu) zaten
`IUnitConverter` ile bu tam kontrolü yapıyor; bu görev aynı deseni
Purchasing'e taşır.

## Owned surface

- `evidence/V1-RMD-239/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Purchasing/OrdersAndReceipts/IPurchasingService.cs
  (V11-PUR-001 sahipliğinde kalır) — `PurchasingService`'e `IStockItemRepository`/
  `IUnitConverter` bağımlılıkları eklenir; `ReceiveGoodsAsync`'in yalnız
  stok-etkisi gönderme adımı (adisyon/receipt kaydının kendisi DEĞİL —
  o hâlâ tedarikçinin kendi biriminde doğru kaydediliyor) dönüştürülmüş
  miktar kullanacak şekilde değişir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Purchasing/PurchasingManagementEndpoints.cs
  (V1-RMD-132 sahipliğinde kalır) — bu Host kompozisyonu `PurchasingService`'i
  kendi `TryAddScoped` setiyle bağımsız olarak kaydediyor (module-registry'ye
  güvenmiyor); yeni iki bağımlılık (`IStockItemRepository`, `IUnitConverter`)
  buraya da eklenmeden endpoint'ler 500 ile patlıyordu — gerçek Host testiyle
  yakalandı.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Purchasing/OrdersAndReceipts/OrdersAndReceiptsDatabaseTests.cs,
  tests/Modules/Purchasing/OrdersAndReceipts/ALKAROS.Purchasing.OrdersAndReceipts.Tests.csproj
  (V11-PUR-001 sahipliğinde kalır) — mevcut `PurchasingService`
  constructor çağrısı iki yeni bağımlılıkla güncellenir; migration 118'in
  (`reorder_point`, `PostgresStockItemRepository.GetByIdAsync`'in artık
  gerçekten okuduğu bir kolon) fixture bağlantısı eklenir — bu satır
  olmadan `StockItemRepository` her çağrıda `42703` ile patlıyordu; yeni
  bir birim-dönüşümü testi eklenir; mevcut testler davranışça değişmez
  (hepsi zaten "kg" hem PO satırında hem stok kaleminde kullanıyor,
  dönüşüm yolu hiçbirinde tetiklenmiyordu ve tetiklenmeyecek).

## In scope

- PO satırının birimi (`line.UnitCode`) stok kaleminin `TrackingUnitCode`'undan
  farklıysa, `IUnitConverter` ile (boyut uyumsuzluğunda fail-closed
  `IncompatibleUnitDimensionException`) dönüştürülür; yalnız dönüştürülmüş
  miktar `ApplyOnHandDeltaAsync`'e ve `StockMovement`'a gider.
- Aynı birimse (çoğunluk durum, ör. "kg"→"kg") davranış hiç değişmez.

## Out of scope

- `GoodsReceiptItem`'in kendi kaydı — hâlâ tedarikçinin gerçek biriminde
  (ör. "kutu") doğru şekilde tutulur; bu, fişte ne teslim alındığının
  gerçek kaydı, değiştirilmez.
- `ProductionStockEffectService.cs`'nin kendi, `IUnitConverter`'dan
  bağımsız ikinci dönüşüm tablosu (ayrı, bağımsız denetimde ayrıca
  bulunan bir bulgu — bu görevin kapsamı dışında, ayrı görev gerektirir).

## Dependencies

- V11-PUR-001

## Acceptance evidence

- Gerçek Postgres testi: stok kalemi "kg" takip ederken PO satırı "kutu"
  biriminde açılıp (`IUnitConverter` içindeki gerçek bir kutu→kg dönüşüm
  faktörüyle) teslim alındığında, on-hand bakiye doğru (kutu sayısı ×
  dönüşüm faktörü) kadar artar — önceden yalnız kutu sayısı kadar artardı.
- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test` (Purchasing OrdersAndReceipts testleri) → yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None

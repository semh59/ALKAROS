# V1-WTR-054 - Paylaşılan terminal kataloğuna kalan satılabilir adet ekle

- Task ID: V1-WTR-054
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`GET /api/v1/terminals/{terminalId}/catalog` (`DualScreenStore.GetCatalogAsync`)
— Garson'un `fetchWholeCatalogAsync`'inin ve Kasa'nın kendi eşdeğerinin
çağırdığı, ikisinin de paylaştığı TEK katalog uç noktası — ürün satırına
`remainingCount: int?` alanını eklemek: doğrudan stok eşlemesi
(`inventory.product_stock_mapping` + `inventory.stock_balances`) olan
ürünler için mevcut stoktan kaç adet daha satılabileceği. Stoku tükenen
(`remainingCount` hesabı 0'a düşen) ürün, `p.is_available = false`
işaretlenmiş bir ürünle AYNI şekilde ele alınır: sonuç setinden tamamen
DÜŞER, `remainingCount: 0` olarak dönmez (2026-09-16, Semih'in kararı —
personel boşa tıklamasın, "neden burada" diye sormasın; en az iş ilkesi).

**Düzeltme notu (bu görev bir önceki taslağın yerine yazıldı):** İlk taslak
yanlışlıkla `TableDraftService`'i (siparişe zaten EKLENMİŞ kalemleri
zenginleştiren, `OrderDtoAssembler.WithAvailableStockAsync` üzerinden —
bu zaten var ve `bill.js`'in "Kalan N" rozetini besliyor, V1-RMD-143) hedef
almıştı. Gerçek boşluk farklı yerde: waiter'ın ÜRÜN SEÇERKEN gördüğü katalog
listesi ayrı bir uç noktadan (`DualScreenStore.GetCatalogAsync`, ham SQL,
hiçbir stok bilgisi taşımıyor) geliyor ve hiç zenginleştirilmiyor.

## Owned surface

- `src/Host/DualScreen/DualScreenStore.cs` (V1-RMD-120'den devralındı, bkz. o
  görevdeki devir notu) — bu görev dosyada yalnız `GetCatalogAsync` ve
  `CatalogRow`'a dokunur, V1-RMD-120 ile V1-RMD-097'nin teslim ettiği sipariş
  yazma yollarını değiştirmez.
- `src/Host/DualScreen/DualScreenContracts.cs` (V1-RMD-009'dan devralındı,
  bkz. o görevdeki devir notu) — bu görev dosyada yalnız `CatalogProductDto`
  record'una alan ekler.
- `evidence/V1-WTR-054/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan):
  - tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs
    (V1-RMD-090 sahipliğinde) — yalnız `GetCatalogAsync`'in yeni alanı için
    test eklenir, mevcut testler değiştirilmez.
  - database/migrations/ altına yeni migration (kendi task-scoped klasöründe,
    ör. database/migrations/V1/V1-WTR-054/**) — bu SQL join'in ihtiyaç
    duyduğu index (`inventory.product_stock_mapping.product_id` üzerinde,
    yoksa) için; mevcut migration dosyaları asla yeniden yazılmaz.

## In scope

1. `GetCatalogAsync`'in SQL'ine `inventory.product_stock_mapping` ve
   `inventory.stock_balances`'a `LEFT JOIN` eklenir (ham SQL stili
   korunur — bu dosya bilerek repository/DI kullanmıyor, bkz. dosyanın
   kendi constructor yorumu); `remainingCount = FLOOR(stock_balance.on_hand
   / mapping.quantity_multiplier)`.
2. Bir ürünün doğrudan stok eşlemesi yoksa `remainingCount = NULL` (stok
   takibi yok, sınırsız kabul edilir — mevcut davranış, regresyon yok).
3. Bir ürünün birden fazla stok eşlemesi varsa (nadiren), `MIN()` alınır —
   `OrderDtoAssembler.WithAvailableStockAsync`'in zaten uyguladığı "sınırlayıcı
   malzeme kararı verir" mantığıyla aynı.
4. Hesaplanan `remainingCount <= 0` olan ürün `WHERE` koşuluna eklenen ek bir
   şartla sonuç setinden düşürülür — `p.is_available` şartıyla birebir aynı
   davranış (ürün hiç dönmez, `0` değeri asla client'a gitmez).
5. Tek sorguda, N+1 yok — mevcut `GetCatalogAsync` zaten tek turda çalışıyor,
   bu davranış korunur; cursor'lu sayfalama mantığı (bir sayfa `limit+1` satır
   çekip fazlasını geri atma) yeni filtreden SONRA uygulanır ki sayfa boyutu
   yanlış hesaplanmasın.

## Out of scope

- Reçete-tabanlı (`ProductRecipeMapping` + `RecipeVersion`, birden çok
  malzemenin `MIN()`'i) hesaplama — doğrudan stok eşlemesinden daha karmaşık
  bir SQL gerektiriyor (malzeme başına join + agregasyon) ve gerçek ihtiyaç
  henüz doğrulanmadı; ayrı bir görev olarak değerlendirilebilir.
- `p.is_available` (Suspend/Restore) boole kontrolü — bu zaten mevcut SQL'de
  var (`WHERE p.is_available`), ürün tamamen listeden düşüyor; bu görev onu
  DEĞİŞTİRMEZ, yalnız kalan SAYIYI ekler.
- Kasa/Cashier istemcisinin kendi gösterimi — aynı uç noktayı paylaştığı için
  bu görev tamamlandığında Kasa'nın API yanıtında da alan hazır olacak, ama
  `cashier-app.js`'in onu render etmesi ayrı, istenirse açılacak bir görev
  (Semih bu turda yalnız Garson'u işaret etti).
- Gerçek zamanlı (SignalR) güncelleme — yalnız istek anındaki değer.

## Dependencies

- V11-INV-002

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 Uyarı, 0 Hata (tüm çözüm).
- `dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj
  --filter "FullyQualifiedName~DualScreenStoreTests"` (gerçek Postgres,
  `ALKAROS_TEST_PG_PORT=55432`) → **12/12 geçti** (8 mevcut + 4 yeni):
  doğrudan eşlemeli düşük stok (2 kaldı → `remainingCount: 2`, ürün listede),
  eşlemesiz ürün (`remainingCount: null`, ürün listede), sıfır stok (ürün
  SONUÇ SETİNDEN TAMAMEN DÜŞER), ve tükenen bir ürünün sayfalamayı
  bozmadığı (page-size-1 isteği hâlâ tam olarak beklenen tek görünür ürünü
  döndürür, hayalet `NextCursor` üretmez).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- Değişiklik yalnız izin verilen üç dosya (git diff ile doğrulandı):
  `DualScreenStore.cs`, `DualScreenContracts.cs`, `DualScreenStoreTests.cs`.
- Planlanan ek migration (yeni index) gerekmedi — `product_stock_mappings`
  tablosunun `PRIMARY KEY (product_id, stock_item_id)`'i zaten `product_id`
  öneki üzerinden aranabilir durumda.
- Hesaplama planın "on_hand" ifadesi yerine `stock_balances.available_quantity`
  kullanıyor (on_hand − reserved) — `OrderDtoAssembler.WithAvailableStockAsync`'in
  (V1-RMD-143) zaten kullandığı otoriter alanla birebir aynı, planın kendi
  metnindeki küçük bir yanlışlık düzeltildi.

## Handoff

- V1-WTR-055
- V1-CUI-010

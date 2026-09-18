# V1-CUI-011 - Kasa modülü için gerçek tarayıcı (E2E) test paketi

- Task ID: V1-CUI-011
- Status: Done
- Assignee: Codex
- Work type: implementation
- Surface state: Planned

## Goal

Bağımsız bir denetim ajanı (2026-09-17, Kasa modülü kapsamlı denetimi),
`tests/E2E/`'nin YALNIZ `WaiterPwa` için gerçek bir Playwright test paketi
içerdiğini, Kasa (Cashier/PosTerminal/CustomerDisplay) için HİÇBİR gerçek
tarayıcı testi bulunmadığını doğruladı. Bu oturumda eklenen HİÇBİR özellik
(ekran koruyucu yükleme/gösterme — V1-CDP-001..004, Faz0 tasarım
migrasyonu — V1-CUI-007..010, kalan-stok rozeti — V1-WTR-054..056/
V1-CUI-010, ikram fiskal düzeltmesi — V1-RMD-228) gerçek bir tarayıcıda
"aç, tıkla, gör" şeklinde doğrulanmadı — yalnız xUnit/HTTP/vitest(jsdom)/
Python statik seviyesinde kanıt var. WaiterPwa'nın kendi E2E paketi daha
önce (V1-WTR-026/027) unit testlerin YAKALAYAMADIĞI gerçek prod bug'ları
(login sonrası tıklanamama, "Gönder" butonunun sessizce başarısız olması)
bulmuştu — aynı sınıf riskler Kasa tarafında da sessizce mevcut olabilir.

## Owned surface

- `tests/E2E/Cashier/**` (yeni — tests/E2E/WaiterPwa (V1-WTR-026) ile aynı
  yapı: `playwright.config.js`, `global-setup.js`, `specs/`, `package.json`)
- `evidence/V1-CUI-011/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenStore.Orders.cs
  (çok sayıda geçmiş dalga görevinin sahipliğinde kalır) — yalnız
  `StartOrderAsync`'in her iki overload'ı artık `servingUserId` parametresi
  alıp `Order`'a geçiriyor. Bu paketin kendi 04 numaralı senaryosu
  ÇALIŞIRKEN bulundu: PosTerminal Cashier'ın "Yeni sipariş aç" akışıyla
  açılan HER siparişte `Order.ServingUserId` null kalıyordu, bu da
  `OrderSubmissionStockDispatcher`'ın attığı bir `InvalidOperationException`
  ile HER kasa satışının gönderiminde 500 hatasına yol açıyordu — gerçek,
  o an üretimde olan bir çökme (V1-WTR-026/027'nin kendi E2E paketinin
  bulduğu bug'ları aynı görevde düzelttiği emsalle aynı desen).
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Endpoints.cs
  (V1-IAM-024 sahipliğinde kalır) — yalnız `POST .../orders` ve
  `POST .../orders/table` uç noktaları artık `RequireCashierPermissionAsync`
  sonucundaki `principal.UserId`'yi `StartOrderAsync`'e geçiriyor; başka
  hiçbir uç nokta/kayıt değişmedi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs
  (V1-RMD-090 sahipliğinde kalır) — yukarıdaki imza değişikliğine uyacak
  şekilde 8 çağrı noktasına `Guid.NewGuid()` eklendi; testlerin kendi
  iddiaları değişmedi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/OrderAggregate/OrderItem.cs
  (V1-RMD-064 sahipliğinde kalır) — yalnız yeni bir `CountsInOrderTotals`
  property'si eklendi (Draft/Active/Complimentary); mevcut `IsActive`
  (Draft/Active) DEĞİŞMEDİ.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/OrderAggregate/Order.cs
  (V1-RMD-064 sahipliğinde kalır) — yalnız `Subtotal`/`DiscountTotal`/
  `TaxTotal`/`Total`'ın kendi `_items.Where(...)` filtresi `i.IsActive`
  yerine `i.CountsInOrderTotals` kullanıyor. Önceden `IsActive`,
  `Complimentary`'yi HARİÇ TUTUYORDU — ikram edilen bir kalem bu dört
  toplamdan da tamamen SİLİNİYORDU, sanki hiç sipariş edilmemiş gibi; bu
  paketin kendi 04 numaralı senaryosuyla bulundu.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/ItemExceptions/ItemExceptionHandler.cs
  (V1-ORD-004 sahipliğinde kalır) — yalnız `ApplyComplimentaryAsync`'in
  kurduğu `compItem`'ın `discountAmount`'ı artık
  `targetItem.NetAmount + targetItem.DiscountAmount` (BillItem.cs'nin
  V1-RMD-228'de kurduğu "gerçek fiyat + ayrı indirim satırı" desenin
  birebir aynısı), önceden değişmeden geçen `targetItem.DiscountAmount`
  (çoğu zaman 0) yerine.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenStore.Display.cs
  (V1-RMD-097 sahipliğinde kalır) — yalnız `GetSnapshotAsync`'in satır
  sorgusu artık `Complimentary` durumundaki kalemleri de döndürüyor
  (önceden `status IN ('Draft','Active')` onları tamamen filtreliyordu) ve
  ikram edilen bir kalemin `LineTotal`'ı, kalıcı `gross_amount`'tan
  (ikramda kasıtlı olarak 0, PDF:I.28.1/V0-DOM-006) yerine
  `unit_price × quantity × (1 + tax_rate/100)`'den yeniden hesaplanıyor —
  Kasa'nın (`Cashier.tsx`) ve müşteri ekranının kendi kalem satırı artık
  gerçek fiyatı gösteriyor, indirim yalnızca `discountTotal` toplamında
  ayrı bir satır olarak görünüyor.

  Yukarıdaki `OrderAggregate`/`ItemExceptionHandler`/`Display.cs` üçü
  birlikte: `ServingUserId` düzeltmesi olmadan gönderilemeyen bir
  siparişte ikram denendiğinde, kalem hem tüm Kasa/müşteri-ekranı
  toplamlarından SİLİNİYOR hem de satırı gösterilse bile ₺0 görünüyordu —
  bu görevin kendi `04-complimentary-line.spec.js` senaryosunun doğrudan
  test ettiği, gerçekten var olan davranış. `dotnet test` ile doğrulanan
  projeler (hepsi yeşil, regresyon yok): `ALKAROS.Orders.OrderAggregate.Tests`
  (128/128), `ALKAROS.Orders.ItemExceptions.Tests` (20/20),
  `ALKAROS.Host.Experience.Orders.Comp.Tests` (14/14), `ALKAROS.Host.Tests`
  (DualScreen dahil, 156/158 — kalan 2 hata `git stash` ile temiz master'da
  da aynen üretilen, bu görevden bağımsız önceden var olan hatalar).

## In scope

- Gerçek Postgres + gerçek Host binary + gerçek Chrome ile çalışan bir
  Playwright test paketi kurmak (WaiterPwa'nın kendi altyapısıyla aynı
  desen: `@playwright/test`, `pg`).
- En az şu senaryoları kapsamak:
  1. Kasiyer girişi → Kasa ekranı açılır, katalog yüklenir.
  2. Bir ürün sepete eklenir, kalan-stok rozeti doğru görünür, sipariş
     mutfağa gönderilir.
  3. Yönetici `/settings/screensaver`'dan bir görsel yükler; müşteri
     ekranı (CustomerDisplay) Idle durumuna geçtiğinde bu görseli gerçekten
     gösterir; görsel kaldırılınca varsayılan markalı karta döner.
  4. Bir ürün ikram edilir; Kasa'nın hesap görünümünde ikramın gerçek
     fiyatıyla + ayrı bir indirim satırı olarak göründüğü (sıfır/görünmez
     satır olmadığı) doğrulanır.

## Out of scope

- WaiterPwa'nın kendi E2E paketini değiştirmek.
- Ödeme/Token entegrasyonu senaryoları — henüz gerçek cihaz/sözleşme yok
  (`V0-HUG-001` Blocked), bu paket yalnız BUGÜN ÇALIŞAN Kasa özelliklerini
  kapsar.

## Dependencies

- V1-CUI-010
- V1-CDP-004
- V1-RMD-228

## Acceptance evidence

- Yeni `tests/E2E/Cashier/` paketi, gerçek Postgres + gerçek Host + gerçek
  Chrome ile çalışır (mock yok).
- Yukarıdaki 4 senaryonun tamamı yeşil geçer.
- README, WaiterPwa'nın kendi E2E README'siyle aynı çalıştırma
  talimatlarını içerir.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None

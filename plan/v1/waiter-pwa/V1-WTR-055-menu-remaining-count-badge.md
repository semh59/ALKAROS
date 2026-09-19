# V1-WTR-055 - Garson menü ekranında kalan adet rozeti

- Task ID: V1-WTR-055
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`menu.js`'in çizdiği ürün kartlarında, V1-WTR-054'ün katalog uç noktasına
eklediği `remainingCount` alanını göstermek: düşük kalan miktarda uyarı
rozeti. Backend akıllı/frontend aptal ilkesi: istemci kendi eşiğini icat
etmez, yalnız sunucunun verdiği sayıyı gösterir. Stoku tükenen ürün için
"Tükendi" durumu YOK — V1-WTR-054 zaten öyle bir ürünü katalog yanıtına hiç
koymuyor (2026-09-16 kararı), dolayısıyla bu görev disabled/"Tükendi" kart
mantığı kurmaz; ürün zaten listede görünmez.

Aynı rozet zaten `bill.js`'in `renderSentLine`'ında var (V1-RMD-143,
`item.availableStockQuantity`, `.product-stock`/`.is-low`/`.is-out` CSS
sınıfları) — ama yalnız sepete EKLENMİŞ kalemlerde gösteriliyor. Bu görev
aynı görsel dili SEÇİM anına (henüz eklenmemiş ürün kartına) taşıyor; yeni
bir görsel dil icat etmiyor, var olanı yeniden kullanıyor.

**Yarış durumu/çakışma güvenliği — ayrı bir iş GEREKMİYOR, zaten var:**
Katalogdaki `remainingCount` yalnız bir ipucudur (istek anının fotoğrafı,
yaşlanabilir — iki garson veya garson+kasiyer aynı anda aynı son adedi
görüp ikisi de eklemeye çalışabilir). Gerçek, yarışa karşı güvenli karar
GÖNDERME/ONAY anında zaten veriliyor: `OrderStockConsumptionService`
`IStockBalanceRepository.TryApplyGuardedOnHandDeltaAsync`'i aynı Postgres
transaction'ı içinde çağırıyor (atomik, koşullu `UPDATE`); stok yetmezse
`null` döner, servis `InsufficientOrderStockException` fırlatır,
`OrderManagementEndpoints.cs` bunu `409 Conflict` +
`"'{ProductName}' için yeterli stok yok."` Türkçe mesajına eşliyor
(V1-RMD-143/144, halihazırda üretimde ve test edilmiş). Yani iki kişi aynı
son adedi görüp ikisi de göndermeye çalışsa, yalnız biri başarılı olur,
diğeri anlaşılır bir Türkçe sebeple reddedilir — bu görev bu davranışı
YENİDEN İNŞA ETMEZ, yalnız kullanıcının bunu SEÇİM anında önceden tahmin
etmesini sağlar.

## Owned surface

- `src/Clients/WaiterPwa/wwwroot/js/screens/menu.js` (V1-WTR-046'dan
  devralındı, bkz. o görevdeki devir notu).
- `evidence/V1-WTR-055/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan):
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-010 ailesinin
    sahipliğinde kalır) — yalnız `loadCatalog()`'un `state.products`
    eşlemesine `remainingCount: product.remainingCount ?? null` satırı
    eklenir; mevcut alan listesi (id/name/price/categoryCode/...)
    değiştirilmez.
  - `tests/Clients/WaiterPwa/Frontend/test_menu_remaining_count.py` (yeni
    dosya) — tests/Clients/WaiterPwa/ dizini onlarca task tarafından
    (V1-RMD-051, V1-IAM-020, V1-WTR-002/003, V1-RMD-002, ...) zaten
    paylaşılan bir yüzey; mevcut alt klasörler/testler değiştirilmez.
- Bu görev src/Clients/WaiterPwa/wwwroot/waiter-app.css'e dokunmaz — yeni
  sınıf eklenmez, `.product-stock`/`.is-low`/`.is-out` zaten var (V1-RMD-143),
  aynen yeniden kullanılır.

## In scope

- `remainingCount` `null` ise hiçbir rozet gösterilmez (mevcut davranış,
  stok takipsiz ürün).
- `remainingCount > 0` ise ürün kartında `bill.js`'in kullandığı aynı
  `.product-stock` işaretlemesi ("Kalan N"), 5'in altında `.is-low`.
- Arama/kategori filtrelemesi mevcut davranışıyla aynı kalır, `remainingCount`
  filtrelemeyi etkilemez.

## Out of scope

- Kasa/Cashier ekranının kendi gösterimi — ayrı görev, bkz. V1-CUI-010.
- `remainingCount`'ın canlı (SignalR) güncellenmesi — yalnız katalog
  yüklendiğinde/yenilendiğinde alınan değer.
- Sepette zaten olan bir satırın miktarını `remainingCount`'ı aştığında geriye
  düşürmek — gerekmiyor, çünkü GÖNDERME anında `OrderStockConsumptionService`
  zaten `TryApplyGuardedOnHandDeltaAsync` ile atomik kontrol ediyor ve
  yetersizse `InsufficientOrderStockException` → `409 INSUFFICIENT_STOCK` →
  "'{ürün}' için yeterli stok yok." ile reddediyor (V1-RMD-143/144,
  halihazırda üretimde). Bu görev yalnız SEÇİM anındaki öngörüyü ekliyor;
  yarış durumuna karşı gerçek koruma zaten var, burada yeniden yazılmıyor.
- Yeni bir kilitleme/rezervasyon mekanizması — `V11-RSV-002` (atomic
  last-portion reservation) diye ayrı bir modül de var ama
  `OrderStockConsumptionService`'in kullandığı yol bu değil; hangisinin
  gerçek çalışma yolu olduğu bu görevin konusu değil, sadece mevcut
  `TryApplyGuardedOnHandDeltaAsync` yoluna güveniliyor.

## Dependencies

- V1-WTR-054

## Acceptance evidence

- Yeni `tests/Clients/WaiterPwa/Frontend/test_menu_remaining_count.py` → 2/2
  geçti: `menu.js`'in rozeti render ettiği, `waiter-app.js`'in
  `remainingCount`'ı `state.products`'a taşıdığı statik olarak doğrulandı.
- `tests/Clients/WaiterPwa/Frontend/test_waiter_pwa_frontend.py` → 3 test
  zaten önceden başarısız (JS modülerleştirme refactor'ünden kalma, bu
  görevle ilgisiz) — `git stash` ile bu görevin değişiklikleri olmadan da
  AYNI 3 testin başarısız olduğu doğrulandı (regresyon değil, önceden var
  olan durum); geri kalan 5 test yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- Değişiklik yalnız izin verilen dosyalar: `menu.js`, `waiter-app.js`
  (yalnız `loadCatalog()`'un eşleme satırı), yeni test dosyası.
- Gerçek tarayıcıda canlı ekran görüntüsü ALINMADI (bu oturumda dev server
  başlatılmadı) — statik kaynak doğrulaması ve `bill.js`'in zaten üretimde
  olan aynı CSS sınıflarının birebir yeniden kullanılması ile güveniliyor.
  Semih'in elle deneyebileceği senaryo: V1-WTR-054'teki test ürününü (2 adet
  kalan) Garson ekranından aç, kart üzerinde "Kalan 2" rozetini gör (aynı
  görsel dil bill.js'teki gibi), stoğu tüketip 0'a düşürdüğünde ürünün
  menüden tamamen kaybolduğunu doğrula.
- Yarış senaryosu (yeni kod yazmadan, yalnız var olan davranışı doğrulama):
  aynı üründen 1 adet kalanken iki farklı terminalden (Garson + Kasa, veya
  iki Garson) aynı anda sepete ekleyip göndermeyi dene — biri başarılı olur,
  diğeri `409 INSUFFICIENT_STOCK` + "'{ürün}' için yeterli stok yok."
  mesajıyla reddedilir; sipariş çift düşmez. Bu, bu görevin ürettiği bir
  davranış değil, V1-RMD-143/144'ün zaten sağladığı garantinin bu yeni
  gösterim özelliğiyle birlikte hâlâ doğru çalıştığının kanıtı.

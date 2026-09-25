# V1-RMD-206 - `waiter-app.test.js` gerçek uygulamayla eşleşmiyordu

- Task ID: V1-RMD-206
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in isteği ("Dokun kök nedeni") — V1-RMD-205'te fark edilen,
`waiter-app.js`'le ilgisiz 3 test hatasının ("tables not rendered yet")
gerçek kök nedeni: `tests/Clients/StaticApps/waiter-app.test.js`, 17
adımlı JS modülerleştirmesinden (V1-WTR-037..053) ve masalar/menü ekranı
yeniden yazımından ÖNCEKİ bir `waiter-app.js` sürümüne karşı yazılmıştı.
Testin varsaydığı `.table-card`/`.product-card` sınıfları ve
`#btnOpenOrderModal`/`#btnSendKitchen` id'leri kod tabanında artık hiç yok
(grep ile doğrulandı) — gerçek işaretleyici `.table`/`.product`
(`data-table`/`data-product` ile) ve `#btnSendFromMenu`/`#btnSendFromBill`.
Uygulama ayrıca `window.alert()`'ten kendi sayfa-içi `toast()`'una geçmiş
(`js/toast.js`), testin hâlâ `alert` mock'ladığı yerde. Dördüncü test
(giriş hata mesajı) gerçekten güncel kalmıştı — o yüzden 4'te 1 geçiyordu.

Ayrıca kod incelenirken (üretim koduna DOKUNULMADAN, yalnız testleri
gerçek akışa göre kurarken) iki gerçek, ilgisiz kusur daha bulundu:

1. `tests/Clients/StaticApps/support/fetchRouter.js`'nin sahte fetch
   yanıtı hiç `headers` taşımıyordu — V1-RMD-205'te zaten düzeltildi.
2. jsdom `CSS.escape`'i hiç desteklemiyor; `waiter-app.js`'in ürün
   eklerken "taze düğmeyi tekrar bul" mantığı (`CSS.escape(product.id)`,
   V1-RMD-174) bunu çağırıyor — gerçek tarayıcılarda sorun değil (evrensel
   destek), ama test ortamında her ürün tıklamasında yakalanmamış bir
   istisnaya yol açıyordu (testler yine de geçiyordu, DOM olay
   dinleyicisi hatayı yutuyor, ama Vitest "unhandled error" olarak
   raporluyor ve gerçek bir hatayı maskeleyebilirdi).

## Owned surface

Sınırlı ek — aşağıdaki yollar V1-RMD-109'un test sahipliğinde kalır
(yollar geri-tik olmadan yazıldı, V1-RMD-111 emsali):

- tests/Clients/StaticApps/waiter-app.test.js — dört testin dördü de
  gerçek akışa (.table/.product seçicileri, #btnSendFromMenu, toast
  metni) göre yeniden yazıldı; regresyon amaçları (V1-RMD-106
  submit-draft, çift-tık koruması, ham HTTP kod sızıntısı, İngilizce ağ
  hatası sızıntısı) aynen korundu.
- tests/Clients/StaticApps/support/setup.js — yeni dosya, jsdom'da eksik
  CSS.escape'in küçük bir polyfill'i, yalnız test ortamı için.
- tests/Clients/StaticApps/vitest.config.js — yeni setupFiles girişi.

## Out of scope

- `waiter-app.js`/`offline-queue.js`/diğer üretim modülleri — hiçbiri
  değişmedi, kusur yalnız testin kendisindeydi (revert-and-confirm ile
  kanıtlandı: üretim kodu geçici olarak bozulup testin gerçekten
  kırıldığı, sonra düzeltilip 19/19'un geri geldiği doğrulandı).
- `cashier-app.test.js`/`offline-queue.test.js` — zaten güncel ve yeşildi.

## Dependencies

- V1-RMD-205

## Acceptance evidence

- `npx vitest run` (tests/Clients/StaticApps) → 19/19 yeşil, sıfır
  "unhandled error".
- Revert-and-confirm: `offline-queue.js`'in `postOrder`'ı geçici olarak
  submit-draft çağırmayacak şekilde bozuldu — ilgili iki test gerçekten
  kırıldı ("submit-draft not called yet"), düzeltme geri alınınca
  19/19'a dönüldü; `git diff` üretim dosyasında hiç kalıntı bırakmadığını
  doğruladı.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.
- Semih'in elle deneyebileceği senaryo: `cd tests/Clients/StaticApps &&
  npx vitest run` — üç dosyanın üçü de yeşil, konsolda hiçbir
  "TypeError"/"Unhandled Errors" bloğu çıkmıyor.

## Handoff

- None

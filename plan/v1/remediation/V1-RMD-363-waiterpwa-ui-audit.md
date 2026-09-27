# V1-RMD-363 - WaiterPwa modül denetimi: toast bildirimlerinde role eksikliği

- Task ID: V1-RMD-363
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-27

## Goal

Modül-modül arayüz denetim sürecinin (plan/v1/ui-audit/UI_AUDIT_PROGRESS.md) 4. modülü:
WaiterPwa (`src/Clients/WaiterPwa/wwwroot/**`, ~4150 satır — `index.html`, `waiter-app.js`, ve
`js/`/`js/screens/`/`js/sheets/` altındaki 18 dosya). On iki boyut üzerinden tarandı.

Bu modül, önceki oturumlarda (2026-09-10 beş-ajanlı Garson denetimi, V1-RMD-166 ila 188, V1-RMD-330,
V1-RMD-340) çok sayıda bağımsız denetimden geçmiş ve Cashier'ın vanilla istemcisinden (Modül 1-3)
belirgin biçimde daha olgun çıktı:

- Native `confirm()/alert()/prompt()` hiç kullanılmıyor.
- `menu.js`/`tables.js`'in sekme benzeri çip düğmeleri zaten `aria-pressed` taşıyor.
- `index.html`'in `#ribbon`/`#pendingBanner`/`#lockSub` alanları zaten `role="status"
  aria-live="polite"` taşıyor; `#loginError` zaten `role="alert"` taşıyor.
- `options-sheet.js`/`waiter-app.js`, Cashier için bu oturumda yeni kurulan modal odak-tuzağından
  (Modül 1) daha gelişmiş, zaten var olan bir odak yönetimi + Escape kapatma sistemine sahip
  (V1-RMD-173/178).
- JS şablon dizgilerindeki 67 benzersiz statik sınıf adının tamamı `waiter-app.css`'te karşılığını
  buldu — Modül 1'in kök nedeni olan CSS/JS sınıf adı uyumsuzluğu (`tab-chip`/`category-tab-btn`)
  sınıfından bir bulgu burada YOK.
- Tüm `js/sheets/*.js` dosyaları (`help-request`, `failed-orders`, `pending-orders`, `party-size`,
  `product-sheet`, `profile`, `transfer`, `void-comp`) tek tek okundu; idempotency-key kalıbı
  (void-sent/comp/transfer-server), sunucu-kaynaklı metin, gerçek zamanlı çift-tıklama korumaları
  (`resolvePendingGuarded`) zaten doğru uygulanmış durumda.

Tek gerçek, bağımsız bulgu: `toast.js`'in paylaşılan `#toasts` kapsayıcısı yalnızca
`aria-live="polite"` taşıyordu; tek tek toast düğümlerinin hiçbirinde `role` yoktu. Bu, ekran
okuyucunun sıradan bir "eklendi" bildirimini gerçek bir hata/uyarıdan ayırt etmesini
engelliyordu — Cashier'ın Modül 1-3'te aynı sınıftan kapattığı boşlukla birebir aynı desende, ama
merkezi tek bir fonksiyonda (`toast()`), her çağıran yerde ayrı ayrı değil.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/WaiterPwa/wwwroot/js/toast.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/WaiterPwa/specs/10-toast-roles.spec.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/ui-audit/UI_AUDIT_PROGRESS.md
- `plan/v1/remediation/V1-RMD-363-waiterpwa-ui-audit.md`

## In scope

1. **[T1, Orta] Tek tek toast düğümleri (`toast()`, `js/toast.js`) hiçbir `role` taşımıyordu.**
   Kapsayıcı (`#toasts`) zaten `aria-live="polite"` idi, bu yüzden ekran okuyucu içeriği yine de
   duyuyordu, ama sıradan bir başarı bildirimi ile gerçekten durup dinlenmesi gereken bir hata
   arasında hiçbir ayrım yoktu. `settings.warning` doğruysa `role="alert"`, değilse
   `role="status"` eklendi — Cashier'ın split-payment/cash-session modüllerinde zaten kurulan
   aynı ayrımın (Modül 2/3) merkezi tek noktası.

## Out of scope

- Bu modülde ek bir ürün-katmanı (P1-P4) gözlemi bulunmadı: rakip karşılaştırması, saha
  gerçekliği, basitlik ve öğrenme eşiği boyutlarında somut, kod değişikliği gerektiren bir açık
  görülmedi — sekme çipleri, sepet/adisyon akışı ve sekme kapatma/PIN kilidi kullanıcı deneyimi
  zaten bu oturumun diğer modüllerine kıyasla en olgun durumda.

## Dependencies

- None

## Acceptance evidence

- `tests/E2E/WaiterPwa` tam paketi (47 test, 10 numaralı yeni dosya dahil): 47/47 geçti, regresyon
  yok.
- Mutation-check: `toast.js` `git stash` ile geri alındı, yeni 10 numaralı spesifikasyon GERÇEKTEN
  kırmızı oldu (`toHaveAttribute('role', ...)` — `role` özniteliği hiç yoktu, "unexpected value
  null"). `git stash pop` ile geri yüklendi, paket tekrar 47/47 yeşile döndü.
- `node --check` ile dosya sözdizimi doğrulandı.
- Test yazımı sırasında iki gerçek düzeltme yapıldı (kendi hatalarım): (1) `secondTableId`
  kullanıldı çünkü `tableId` üzerinde 03-waiter-actions.spec.js zaten bir `/help-requests` çağrısı
  yapmıştı — aynı masanın 2 dakikalık sunucu bekleme süresiyle (V1-WTR-014) çakışabilirdi;
  (2) tıklamadan sonra hangi ekranın (adisyon sayfası mı, menü mü) açılacağı önceki testlerin
  bıraktığı duruma bağlı olduğundan, tek bir locator'ı beklemek yerine `Promise.race` ile ikisi
  arasında gerçek bir yarış durumu doğru şekilde ele alındı.

## Handoff

- None

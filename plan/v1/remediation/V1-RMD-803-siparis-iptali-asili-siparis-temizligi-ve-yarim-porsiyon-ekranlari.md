# V1-RMD-803 - Arayüz: hesap iptalinde "siparişi de iptal et", kasa kuyruğundan sipariş iptali, yönetici asılı sipariş temizliği, yarım porsiyon bayrağı

- Task ID: V1-RMD-803
- Status: InProgress
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin G5 grubunun ekran tarafı (`7-birlesik-dogrulama.md` satır 5 ve 13). Sunucu işleri V1-RMD-800, 801 ve 802'de bitti; bu görev onları ekrana bağlar. Tüm metinler Türkçe, ham sunucu metni ve HTTP kodu basılmaz, hata kodları sabit cümlelere çevrilir (`docs/UI_STYLE_GUIDE.md`).

1. Hesap iptal akışı (kasa tahsilat sayfası `split-payment.js` ve React `RefundPanel`): "Siparişi de iptal et" kutusu (varsayılan kapalı) ve açıklama: hazırlanmış kalem fire sayılır, hazırlanmamış kalemin stoğu geri gelir, masa boşalır; kutu işaretliyse istek `cancelOrder: true` taşır ve sonuçta `orderCancelled` gösterilir.
2. Kasa kuyruğundan sipariş iptali (`PendingChecksWorkspace`): her hesap kartında gerekçeli, iki adımlı onaylı "Siparişi iptal et"; sipariş okunup güncel sürümüyle `POST .../orders/{id}/cancel` çağrılır; liste yenilenir.
3. Yönetici "asılı sipariş temizle" (yönetim, güvenlik ve sistem bölümü): `GET /security/orders/hanging` listesi (sipariş no, durum, yaş, masa, stoğa dönecek ve fire olacak kalem sayısı), listeden önce önizleme (`dryRun`), gerekçeyle ve önizlemede görülen sipariş kimlikleriyle (`orderIds`, sunucuda zorunlu) temizleme ve siparişin tek tek sonucu (temizlendi, değişmiş, ödeme var, servis edilmiş ürün var).
4. Yarım porsiyon: ürün formunda "Yarım porsiyon olur" kutusu (oluşturma ve düzenleme); WaiterPwa menüden gelen `allowsHalfPortion` bayrağını okur: bayraksız üründe "yarım" düğmesi görünmez, ürün sayfasında 0,5 hızlı seçimi yoktur ve taslak satırında yarımlık artırma/azaltma yerine tam adet adımı kullanılır; bayraklı üründe mevcut yarım adım korunur.
5. İstemci hata sözlükleri: `ORDER_QUANTITY_NOT_ALLOWED`, `REFUND_QUANTITY_NOT_WHOLE` ve yeni iptal/temizleme kodları (`ORDER_HAS_PAYMENT`, `ORDER_CANCEL_NOT_ALLOWED`, `ORDER_HAS_SERVED_ITEMS`, `REASON_REQUIRED`, `STOCK_RESTORE_FAILED`, `CONCURRENCY_CONFLICT`) Türkçe sabit cümlelerle.

`split-payment.js` şu an V1-RMD-825 (G7/G8) tarafından düzenleniyor; bu görev o görev `Done` olmadan başlamaz (bağımlılık). G7'nin dosyası temiz olduğunda ilk iş ona dokunulur; çakışma çıkarsa G7'nin dosyasına dokunulmaz, bağlantı noktası RESULT.md'ye yazılır.

## Owned surface

- `evidence/V1-RMD-803/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/billing/RefundPanel.tsx (V1-RMD-366) — siparişi de iptal et kutusu.
  - src/Clients/PosTerminal/src/features/billing/billing.css (V1-RMD-510) — iptal kutusu satırı (48 px dokunma hedefi).
  - src/Clients/PosTerminal/src/features/billing/RefundPanel.test.tsx (V1-RMD-366) — iptal çağrısının yeni gövdesi.
  - src/Clients/PosTerminal/src/features/billing/refundApi.ts (V1-RMD-366) — iptal isteği bayrağı, hata cümleleri.
  - src/Clients/PosTerminal/src/features/billing/refundApi.test.ts (V1-RMD-366) — yeni hata kodları.
  - src/Clients/PosTerminal/src/features/billing/billingApi.ts (V1-RMD-366) — hata sözlüğü.
  - src/Clients/PosTerminal/src/features/billing/billingApi.test.ts (V1-RMD-366) — hata sözlüğü testi.
  - src/Clients/PosTerminal/src/strings.ts (V1-RMD-097) — yeni cümleler.
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.tsx (V1-RMD-281) — sipariş iptali.
  - src/Clients/PosTerminal/src/features/pending-checks/pendingChecksApi.ts (V1-RMD-281) — sipariş okuma ve iptal çağrısı.
  - src/Clients/PosTerminal/src/features/pending-checks/pending-checks.css (V1-RMD-281) — iptal alanı.
  - src/Clients/PosTerminal/src/features/management-security-system/SecuritySection.tsx (V1-RMD-450) — asılı sipariş paneli.
  - src/Clients/PosTerminal/src/features/management-security-system/api.ts (V1-RMD-450) — liste ve temizleme çağrıları.
  - src/Clients/PosTerminal/src/features/management-security-system/models.ts (V1-RMD-450) — tipler ve sonuç cümleleri.
  - src/Clients/PosTerminal/src/features/management-security-system/management-security-system.css (V1-RMD-450) — panel yerleşimi.
  - src/Clients/PosTerminal/src/features/management-security-system/SecuritySection.test.tsx (V1-RMD-450) — panel bağlantısı.
  - src/Clients/PosTerminal/src/features/management-security-system/TerminalsPanel.test.tsx (V1-RMD-450) — güvenlik bölümü testinin sahte istemcisine asılı sipariş listesi.
  - src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.tsx (V1-RMD-784) — yarım porsiyon kutusu.
  - src/Clients/PosTerminal/src/features/catalog/catalog.css (V1-RMD-076) — yarım porsiyon kutusu satırı (48 px dokunma hedefi).
  - src/Clients/PosTerminal/src/features/catalog/models.ts (V1-RMD-102) — ürün alanı.
  - src/Clients/PosTerminal/src/features/catalog/catalogApi.ts (V1-RMD-102) — oluşturma ve ayrıntı güncelleme gövdesi.
  - src/Clients/PosTerminal/src/features/catalog/catalogApi.test.ts (V1-GOV-038) — yeni alanın gövdesi.
  - src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.test.tsx (V1-GOV-038) — mevcut testlerin yeni alana uyumu.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-RMD-083) — bayrağı okuma, yarım düğmesi ve adım.
  - src/Clients/WaiterPwa/wwwroot/js/screens/menu.js (V1-WTR-055) — yarım düğmesi yalnız bayraklı üründe.
  - src/Clients/WaiterPwa/wwwroot/js/sheets/product-sheet.js (V1-WTR-046) — 0,5 hızlı seçimi yalnız bayraklı üründe.
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js (V1-RMD-264) — iptal bölümü ve hata sözlüğü.
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css (V1-RMD-443) — onay kutusu satırı.
  - tests/Host/Experience/CashSession/ClientLimitLockTests.cs (V1-RMD-794) — uzunluk kilidi sunucudan kopyalanan hata cümlelerini saymaz (onları hata sözlüğü kilidi birebir denetler).
  - src/Clients/PosTerminal/src/features/management-security-system/HangingOrdersPanel.tsx (V1-RMD-450) — yeni dosya: asılı sipariş paneli.
  - src/Clients/PosTerminal/src/features/management-security-system/HangingOrdersPanel.test.tsx (V1-RMD-450) — yeni dosya: panel testleri.
  - src/Clients/PosTerminal/src/features/billing/RefundPanel.cancel-order.test.tsx (V1-RMD-366) — yeni dosya: iptal kutusu testleri.
  - src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.cancel.test.tsx (V1-RMD-281) — yeni dosya: kuyruktan iptal testleri.
  - src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.half-portion.test.tsx (V1-RMD-784) — yeni dosya: yarım porsiyon kutusu testleri.
  - tests/Clients/StaticApps/split-payment-cancel-order.test.js (V1-RMD-109) — yeni dosya: kasa iptal bölümü testleri.
  - tests/Clients/StaticApps/waiter-half-portion.test.js (V1-WTR-055) — yeni dosya: garson menü ve ürün sayfası davranış testleri (gerçek modüller, jsdom).
  - tests/Clients/StaticApps/waiter-half-portion-stepper.test.js (V1-WTR-055) — yeni dosya: taslak satırı adım davranışı (gerçek uygulama, jsdom).
  - tests/Clients/WaiterPwa/Frontend/test_waiter_half_portion.py (V1-WTR-055) — yeni dosya: garson yarım porsiyon testleri.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her kabul koşulu için önce kırmızı test (vitest, jsdom, Python/JS statik testler), sonra düzeltme, sonra yeşil; mutasyon turu (`cp` + `cmp` ile geri alınır).
2. İngilizce kod ve test adları; ekrana basılan her metin Türkçe; `docs/UI_STYLE_GUIDE.md` §4 İngilizce sızıntı taraması dokunulan dosyalarda temiz.
3. Yükleniyor, hata, boş, çift gönderim ve eski sürüm (409) durumları iptal ve temizleme ekranlarında ele alınır; dokunma hedefleri 48 px.
4. Görsel doğrulama: Playwright ile 360-768-1280 genişlikte (yoksa jsdom ve CSS ölçümü; RESULT.md'de açıkça yazılır).

## Out of scope

- Sunucu, veritabanı, geçiş; G7/G8 ekran işleri (satış ekranı ilk dokunuş, tahsilat sayfası hiyerarşisi); React satış ekranında kesirli adet (satış ekranı tam adetle çalışır).

## Dependencies

- V1-RMD-802
- V1-RMD-825

## Acceptance evidence

1. Kırmızı sonra yeşil, gerçek exit code ile `evidence/V1-RMD-803/`: PosTerminal vitest (billing, pending-checks, management-security-system, catalog), `tsc --noEmit`, Cashier statik testleri, WaiterPwa Python testleri.
2. Mutasyon: bayrak gövdeye girmezse, onay adımı atlanırsa, sürüm gönderilmezse, bayraksız üründe yarım düğmesi görünürse, hata kodu sözlükten düşerse, dryRun yerine doğrudan temizlenirse en az bir test kırılır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. qa-frontend ve qa-ux-tasarim incelemeleri sırayla, bulgular giderilir, düzeltmelerden sonra doğrulama turu.
5. Semih'in elle deneyeceği senaryo: kasada bir hesabı aç, "hesabı iptal et" ve "siparişi de iptal et" işaretle: sipariş ve hesap kaybolur, masa boşalır; kasa kuyruğundaki hesapsız bir siparişi gerekçeyle iptal et; yönetimde asılı sipariş listesinden önizle ve temizle; ürün formunda "yarım porsiyon olur"u işaretle: garson uygulamasında yalnız o üründe yarım düğmesi çıkar.

## Handoff

- None

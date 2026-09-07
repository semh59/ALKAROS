# V14-NFC-003 - Anonymous menu read and the NFC customer order page

- Task ID: V14-NFC-003
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-07

## Goal

`V14-NFC-001`'in arkasına, müşterinin NFC dokunuşuyla açtığı gerçek bir
ekran koymak: menüyü görsün, sepetini oluştursun, siparişi göndersin ve
sonucu (mutfağa iletildi) görsün — Semih'in "hem backend hem frontend"
talebini NFC kanalı için tamamlamak.

## Owned surface

- `src/Clients/PosTerminal/src/routes/NfcOrder.tsx` (yeni)
- `src/Clients/PosTerminal/src/routes/NfcOrder.test.tsx` (yeni)
- `src/Clients/PosTerminal/src/routes/nfc-order.css` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - src/Host/Experience/NfcOrdering/**, tests/Host/Experience/NfcOrdering/**
    (V14-NFC-001 sahipliğinde) — anonim `GET {tableId}/catalog` endpoint'i
    eklendi (mevcut `DualScreenStore.GetCatalogAsync`'i olduğu gibi
    kullanıyor, yeniden yazmıyor); test veritabanı fixture'larına
    `V1-GOV-040`'ın `is_available` migration'ı eklendi.
  - src/Clients/PosTerminal/src/api.ts, contracts.ts, App.tsx, index.html
    (birikimli olarak birçok görevin sahipliğinde, en son V1-CUI-006) —
    yalnız yeni `nfcCatalog`/`placeNfcOrder` çağrıları, `NfcOrderResult`
    tipi, `/nfc/` route'u ve Inter font link'i eklendi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Anonim, oturumsuz katalog okuma (mevcut terminal-scoped catalog
  endpoint'inin aynı sorgusu, yalnız oturum zorunluluğu olmadan).
- `/nfc/{tableId}` müşteri sayfası: menü listesi, adet seçici (stepper),
  sepet çubuğu, gönderim, onay ekranı; `TABLE_NOT_AVAILABLE`/masa
  bulunamadı/ağ hatası durumlarının backend'in ürettiği Türkçe mesajla
  gösterilmesi.
- Faz 0 marka kimliğinin (`docs/design/foundations.md`) bu sayfaya
  uygulanması — sayfaya özel, paylaşılan `tokens.css`'e dokunmayan yerel
  CSS değişkenleriyle (bkz. Acceptance evidence'taki gerekçe).

## Out of scope

- Paylaşılan `design-system/tokens.css`'in yeni marka paletine
  taşınması — bu sayfanın kapsamı dışında, ayrı ve daha büyük bir görev
  (aşağıya bakın).
- Yaş kısıtlı ürün istisnası (`V14-NFC-002`).
- QR kanalının kendi müşteri sayfası (`V14-CWB-*`, hâlâ relay'e bağlı).

## Dependencies

- V14-NFC-001

## Deliverables

- `src/Clients/PosTerminal/src/routes/NfcOrder.tsx` ve testleri.
- Anonim katalog okuma endpoint'i.

## Acceptance evidence

- Semih'in elle deneyebileceği senaryo: `/nfc/{tableId}` açılır, menü
  görünür, bir ürün sepete eklenir, "Siparişi Gönder" ile gönderilir,
  "Siparişiniz alındı" onay ekranı gerçek sipariş kalemleri ve toplamla
  görünür.
- `TABLE_NOT_AVAILABLE` (Reserved/Cleaning/OutOfService masa) durumunda
  sayfa backend'in ürettiği "garsonu çağırın" mesajını gösterir, ham hata
  kodu/HTTP durumu asla ekrana basılmaz (`docs/UI_STYLE_GUIDE.md`).
- **Önemli bulgu, kayda geçirildi:** `design-system/tokens.css`'in
  `--ds-color-accent`'i bugün ~15 yerde açık zeminde düz metin/ikon rengi
  olarak kullanılıyor (catalog/tables/kitchen-operations kicker'ları,
  login eyebrow'u). Bu token'ı doğrudan yeni marka rengine (`#00CFFF`)
  çevirmek, foundations.md'nin zaten ölçtüğü WCAG başarısızlığını
  (1.71-1.85:1) tüm bu ekranlara sessizce yayardı. Bu yüzden bu sayfa
  paylaşılan token dosyasına dokunmadı; kendi yerel `--nfc-*`
  değişkenlerini foundations.md'deki değerlerle birebir tanımladı. Paylaşılan
  `tokens.css`'in gerçek migrasyonu (her ~15 kullanım yerinin tek tek
  fill/text olarak sınıflandırılması) ayrı, dikkatli bir görev olarak
  kalır — hangi modül ekranı gerçekten yeniden yapılırken (ör. Kasa/Garson)
  o görev açılmalı, bu sayfanın yan etkisi olarak yapılmadı.
- `node_modules/.bin/tsc --noEmit`: hata yok.
- `node_modules/.bin/vitest run`: 20 dosya, 127 test, hepsi geçti (yeni 4
  test dahil, regresyon yok).
- `node_modules/.bin/vite build`: başarılı.
- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- Yeni backend testi (`TheCatalogEndpointListsAvailableProductsWithoutAnySession`)
  dahil `ALKAROS.Host.Experience.NfcOrdering.Tests`: gerçek Postgres'e
  karşı 9/9 geçti (yerel port 55432 ile hızlı doğrulama; tam docker
  compose test suite ile de doğrulandı).

## Handoff

- None

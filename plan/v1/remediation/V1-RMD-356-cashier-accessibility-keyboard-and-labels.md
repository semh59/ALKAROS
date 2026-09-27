# V1-RMD-356 - Kasa: klavye ile ürün seçimi ve form alanlarının programatik adları

- Task ID: V1-RMD-356
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) düşük seviye bulgusu: vanilla Kasa (Cashier) istemcisinin statik
sayfalarında üç erişilebilirlik boşluğu vardı.

1. `.pos-product-card` zaten `tabindex="0" role="button"` taşıyordu (Tab ile odaklanılabiliyordu) ama hiçbir
   yerde Enter/Space dinlenmiyordu — her klavye kullanıcısının ve ekran okuyucunun odaklanmış bir düğmeyi
   etkinleştirmek için beklediği iki tuş.
2. Miktar düğmeleri ("−"/"+") sembol-only'di, kardeşi olan "Sil" düğmesinin aksine `aria-label` taşımıyordu —
   ekran okuyucu hangi kalemi etkilediğini söyleyemiyordu.
3. `cash-session.js`'te görünür `<label>` etiketleri `for` özniteliği taşımıyordu (yalnızca göze bağlıydı);
   `split-payment.js`'te `<span class="sp-field-label">` bütün bir alan GRUBUNU (örn. "İndirim / düzeltme ekle")
   tanımlıyordu, tek bir input'a hiç bağlanmamıştı — `getByLabel`/bir ekran okuyucu bu alanların hiçbirini
   çözemiyordu.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/index.html
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/21-accessibility-keyboard-and-labels.spec.js
- `plan/v1/remediation/V1-RMD-356-cashier-accessibility-keyboard-and-labels.md`

## In scope

1. `cashier-app.js`: `el.productMatrix`'e mevcut `click` dinleyicisiyle birebir aynı mantığı taşıyan bir
   `keydown` dinleyicisi eklendi (Enter/Space → `addProductToTicket`). `dec`/`inc` düğmelerine, kalemin adını
   taşıyan `aria-label` (`"${item.name} adedini azalt/artır"`) eklendi.
2. `index.html`: `#searchInput`'a kalıcı bir `aria-label` eklendi (placeholder yazıldıktan sonra kaybolur, o
   yüzden tek başına yeterli bir ad değildir).
3. `cash-session.js`: sekiz `<label class="cs-field-label">`'in tamamına, hemen altındaki input/textarea'nın
   `id`'siyle eşleşen `for` özniteliği eklendi (`username`, `password`, `opening-balance`, `movement-amount`,
   `movement-notes`, `counted-amount`, `count-notes`, `override-reason`).
4. `split-payment.js`: grup-seviyeli `<span>` etiketiyle bağlantısız kalan 11 alana (`decision-note`,
   `claim-slip`, `resolve-reason`, `discount-reason`, `discount-calc-type`, `discount-value`, `discount-note`,
   `tip-amount`, `tip-note`, `split-count`, `amount-draft`, `note-draft`) doğrudan `aria-label` eklendi.

## Out of scope

1. `src/Clients/PosTerminal/dist/cashier/**` — derlenmiş bir build çıktısı; bir sonraki `corepack pnpm build`
   onu otomatik günceller.

## Dependencies

- None

## Acceptance evidence

- `tests/E2E/Cashier/specs/21-accessibility-keyboard-and-labels.spec.js` (YENİ, gerçek Chromium + gerçek Host +
  gerçek Postgres'e karşı, 3 test): (1) bir ürün kartına SADECE klavyeyle (fare tıklaması hiç yok) odaklanıp
  Enter ve Space ile iki kez sepete eklendiğini, miktar düğmelerinin `getByRole('button', { name: '<kalem adı>
  adedini azalt/artır' })` ile çözülebildiğini, arama kutusunun `getByLabel` ile çözülebildiğini kanıtlıyor;
  (2) Kasa Oturumu giriş ekranının `getByLabel('Kullanıcı adı')`/`getByLabel('Şifre')` ile, açılış ekranının
  `getByLabel('Açılış Tutarı')` ile çözülebildiğini kanıtlıyor; (3) Tahsilat ekranındaki indirim/bahşiş
  alanlarının tamamının `getByLabel` ile çözülebildiğini kanıtlıyor. 3/3 geçti.
- Regresyon: aynı çalıştırmada `specs/18-discount-and-tip.spec.js` (3/3) birlikte çalıştırıldı — 6/6 geçti.
- Mutation-check: dört dosyadaki TÜM düzeltmeler (keydown dinleyicisi, `aria-label`'lar, `for` öznitelikleri)
  aynı anda geri alındı, yeni testin 3 test durumunun TAMAMI GERÇEKTEN kırmızı oldu (ürün kartı klavyeyle
  etkinleşmedi, miktar düğmeleri adsız kaldığından `getByRole` bulamadı, `getByLabel` çağrılarının hiçbiri
  hiçbir alanı çözemedi — biri 30 saniyelik zaman aşımına, ikisi görünürlük zaman aşımına uğradı). Dosyalar
  yedekten geri yüklendi, `git diff --stat` ile satır sayılarının orijinal düzeltmeyle birebir eşleştiği
  doğrulandı, test paketi tekrar 6/6 yeşile döndü.

## Handoff

- None

# V1-RMD-355 - Bekletilen fiş geri yükleme onayı artık native confirm() değil uygulama içi modal kullanıyor

- Task ID: V1-RMD-355
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) düşük seviye bulgusu: `cashier-app.js`'in `recallParkedTicket`
fonksiyonu, mevcut sepet doluyken bekletilen bir fişi geri yüklemeden önce native, tarayıcının kendi
stillendirilemeyen `confirm()` kutusunu kullanıyordu. Bu depo bu TÜRDEN bir sorunu ZATEN bir kez çözmüştü
(`V1-RMD-253`, `alert()` → kendi kendini kapatan toast) — `confirm()` de aynı sınıftan bir sorun: markaya
uymayan bir kutu, ekran okuyucu için tutarsız, ve (bu görevin kendi E2E testinin de kanıtladığı gibi) otomatik
bir tarayıcı testinde `page.on('dialog')` işleyicisi olmadan sessizce reddediliyor.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.js
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/index.html
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/cashier-app.css
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/20-parked-ticket-recall-confirm.spec.js
- `plan/v1/remediation/V1-RMD-355-cashier-parked-ticket-confirm-modal.md`

## In scope

1. `index.html`: `#parkedModal`'ın aynı `.modal-overlay`/`.modal-card` desenini paylaşan yeni bir `#confirmModal`
   (`role="alertdialog"`, "Vazgeç"/"Devam Et" butonları).
2. `cashier-app.css`: `.modal-card--confirm`/`.modal-card__message`/`.modal-card__actions` küçük stil ekleri.
3. `cashier-app.js`: yeni `showConfirmModal(message)` — modalı gösterip butonlardan birine tıklanana kadar
   çözülen bir `Promise<boolean>` döndürüyor (`V1-RMD-253`'ün toast deseniyle aynı, kendi kendine yeten şekil).
   `recallParkedTicket` `async` oldu, `confirm(...)` çağrısı `await showConfirmModal(...)` ile değiştirildi.

## Out of scope

1. `src/Clients/PosTerminal/dist/cashier/**` — derlenmiş bir build çıktısı, kaynak değil; bir sonraki
   `corepack pnpm build` onu otomatik olarak günceller.

## Dependencies

- None

## Acceptance evidence

- `tests/E2E/Cashier/specs/20-parked-ticket-recall-confirm.spec.js` (YENİ, gerçek Chromium + gerçek Host +
  gerçek Postgres'e karşı): sepet doluyken geri yükleme denendiğinde `#confirmModal`'ın gerçekten göründüğünü,
  doğru Türkçe metni taşıdığını, `role="alertdialog"` olduğunu, "Vazgeç"in sepeti DEĞİŞTİRMEDEN modalı
  kapattığını, ve "Devam Et"in sepeti bekletilen fişle değiştirip her iki modalı da kapattığını kanıtlıyor.
  1/1 geçti.
- Regresyon: `specs/02-stock-badge-and-kitchen-dispatch.spec.js` (3/3 dahil) ve `specs/05-cash-session-lifecycle.spec.js`
  (aynı `alkaros_cashier_parked` localStorage anahtarına dokunan komşu testler) birlikte çalıştırıldı — 5/5 geçti.
- Mutation-check: `recallParkedTicket`'ın gövdesi geçici olarak eski `confirm(...)` çağrısına geri döndürüldü
  (dosyanın kalanı — yeni `#confirmModal` markup'ı ve `showConfirmModal` fonksiyonu — değişmeden bırakıldı) ve
  yeni test GERÇEKTEN kırmızı oldu: Playwright, işlenmemiş native `confirm()`'ü otomatik reddettiğinden
  `#confirmModal` hiçbir zaman görünür olmadı (`Received: hidden`, `locator.toBeVisible()` 8 saniye sonra zaman
  aşımına uğradı) — testin gerçekten bu davranışı doğruladığını kanıtlıyor. Değişiklik geri alındı, `diff` ile
  dosyanın orijinaliyle bayt-eşit olduğu doğrulandı, test tekrar yeşile döndü.

## Handoff

- None

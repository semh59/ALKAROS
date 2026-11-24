# ALKAROS device / browser test plan

> Source basis: EXT:WCAG-2.2, PO:2026-09-01
> Owner task: V1-RMD-092
> Accessibility target authority: `docs/compliance/accessibility-target.md` (V0-CMP-005)

## 1. Target matrix

From `docs/compliance/accessibility-target.md` §4, the surfaces ALKAROS V1 must
be verified on:

| Client | Device | OS | Browser | Screen reader |
| --- | --- | --- | --- | --- |
| Cashier POS (`src/Clients/Cashier`) | Touch-screen POS | Windows 11 | Chrome (kiosk mode) | N/A (touch-only kiosk) |
| Waiter PWA (`src/Clients/WaiterPwa`) | Handheld tablet | Android 14 | Chrome | TalkBack |
| PosTerminal (`src/Clients/PosTerminal`) | Back-office / manager desktop | Windows 11 / macOS | Chrome / Firefox / Edge | NVDA / VoiceOver |
| DualScreen customer display | Fixed screen | Windows 11 | Chrome (kiosk) | N/A (display-only) |

WCAG target: **2.2 Level AA** for every surface, with one approved exception
(EXC-001: Cashier POS waives 2.4.11 Focus Appearance for touch-only kiosk mode).
The level decision (V0-CMP-005-D001) and EXC-001 were approved on 2026-09-01 by
Semih (Founder / Product Owner) — see `docs/compliance/accessibility-target.md`.

### Responsive breakpoints (PosTerminal shell)

`classifyViewport(px)` → `mobile` (< 768), `compact` (768–1279), `wide` (≥ 1280).
Below the wide breakpoint the context panel collapses into an explicit sheet.

## 2. Automated coverage

Run with `pnpm --dir src/Clients/PosTerminal test` (part of the wave reseal gate).

| Area | Test | What it checks |
| --- | --- | --- |
| Shell reflow | `src/shell/ProductionShell.test.tsx` | `classifyViewport` at 320 / 767 / 768 / 1279 / 1280 / 1920 px; context becomes a sheet below `wide`; focus moves to the sheet close button; Escape closes it |
| Shell a11y | `src/shell/ProductionShell.test.tsx` | axe: no critical/serious violations |
| Design primitives a11y | `src/design-system/primitives.test.tsx` | axe on the primitive set |
| Catalog / Tables / Floor plan / Kitchen / Billing a11y | `src/features/*/**.test.tsx` | axe on each workspace |
| Stale-shell a11y | `src/stale.test.ts` | axe on the offline/stale shell |
| Vanilla client shells | `src/vanilla-clients-a11y.test.ts` | Cashier + Waiter PWA `index.html`: `lang`, `title`, zoom-permitting viewport meta, a landmark, and axe (no critical/serious) on the shipped shell markup |

**Contrast (WCAG 1.4.3 / 1.4.11):** axe's `color-contrast` rule is disabled in
these tests because jsdom cannot compute rendered colours. Contrast is instead
enforced by the design-system tokens (`src/Clients/PosTerminal/src/design-system`)
and re-checked on real devices in the manual pass below.

**Dynamic state of the vanilla clients:** the smoke test covers the static shell
only (before `cashier-app.js` / `waiter-app.js` render). Rendered-state a11y for
those clients is covered by the manual checklist and by the fact that their DOM
patterns mirror the PosTerminal workspaces that do have full axe coverage.

## 3. Manual pre-go-live checklist (physical devices)

Automation cannot replace a real device, real touch, a real screen reader, or a
real printer. Before go-live, on the actual hardware from the matrix:

### Cashier POS (Windows 11 touch-screen, Chrome kiosk)

- [ ] Kiosk-mode Chrome full-screen; no browser chrome reachable.
- [ ] Every primary action reachable with one thumb tap; targets ≥ 44 × 44 px.
- [ ] Barcode scanner input focuses the search field and submits.
- [ ] Pinch-zoom works (text can be enlarged) — WCAG 1.4.4.
- [ ] Order dispatch → kitchen ticket prints on the real printer.
- [ ] Park / recall a ticket; totals and item list survive.
- [ ] Pull the network cable: the client fails closed (no silent mock catalog).

### Kasa çekmecesi (gerçek cihaz, Semih'in deneyeceği adımlar)

**Önce uyarı: bu özellik varsayılan olarak KAPALIDIR. Üretimde, bu denemeyi yapmadan açılmaz.**

Gerekenler: çekmece kablosu (RJ11/RJ12) bağlı bir fiş yazıcısı, yazıcının ağ adresi (IP) ve ham yazıcı portu (çoğu yazıcıda 9100), bir kasa oturumu ve bir açık hesap.

| Adım | Ne yapılır | Beklenen sonuç |
|---|---|---|
| 1 | Çekmece kablosunu yazıcının "çekmece" (DK/Drawer) çıkışına takın; yazıcıyı ağa bağlayın, adresini yazıcının kendi durum fişinden okuyun. | Yazıcı ağda görünür; başka bir bilgisayardan `ping` yanıt verir. |
| 2 | Yönetim > Ayarlar'da "Kasa yazıcısı adresi" için Düzenle'ye basın, `adres:port` yazın (örn. `192.168.1.50:9100`), gerekçe yazıp kaydedin. | "Ayar kaydedildi."; adres listede görünür. Hatalı biçimde Türkçe uyarı çıkar, kaydedilmez. |
| 3 | "Kasa çekmecesini yazıcıdan aç" ayarını Evet yapın (gerekçe: "Cihaz denemesi"). | Ayar listesinde "Evet". |
| 4 | Kasada oturum açın; nakit ödeme alın (bir hesabı nakit tahsil edin). | Ödeme biter ve çekmece kendiliğinden açılır. Kart ve havale ödemesinde çekmece AÇILMAZ. |
| 5 | Ödeme bittikten sonra ekranı yenileyip hesabı yeniden açın. | Çekmece kendiliğinden yeniden açılmaz (çift gönderim denemesi geliştirici testindedir). |
| 6 | Kasa oturumu ekranında "Satış yok"a basın; "Bozuk para" seçip "Çekmeceyi aç"a basın. | "Çekmece açıldı." çıkar, çekmece açılır; "Bu vardiyada satışsız açma: 1" görünür; kasadaki beklenen tutar DEĞİŞMEZ. |
| 7 | "Satış yok" > "Sayım", sonra "Diğer" (not yazmadan deneyin, sonra notla). | Notsuz "Diğer" reddedilir ("“Diğer” için not yazın."); notla açılır. |
| 8 | Aynı oturumda beş kez "Satış yok" yapın. | Beşincide Türkçe uyarı çıkar ("Bu vardiyada 5 kez satışsız çekmece açıldı."). |
| 9 | Kasayı sayıp kapatın; yönetici denetim kaydında kapanış olayına bakın (veya geliştiriciden kapanış kaydındaki `drawerSaleOpenCount`, `drawerNoSaleCount`, `drawerNoSaleKickFailedCount`, `drawerSaleKickFailedCount` sayılarını isteyin). | Sayılar yaptığınız denemelerle eşleşir: açılamayan satışsız denemeler ile ödeme sırasında açılamayan çekmece sayısı ayrı sayılır. |
| 10 | Ayarı Hayır yapın ve nakit ödeme ile "Satış yok"u yeniden deneyin. | Ödeme normal alınır, çekmece açılmaz; "Satış yok" düğmesi görünmez. |

Sorun giderme:

- **`ping` yanıt veriyor ama çekmece açılmıyor**: `ping` yalnız yazıcının ağda olduğunu gösterir; ham yazıcı portunun (çoğu yazıcıda 9100) açık olduğunu göstermez, yazıcı ayarından kontrol edin.
- **Çekmece açılmıyor, ekranda "Çekmece açılamadı; yazıcı bağlantısını kontrol edin."**: yazıcı kapalı veya adres/port yanlış; sunucu bilgisayarından yazıcının adresine ve portuna ulaşılabildiğini doğrulayın. Sunucu Docker içinde çalışıyorsa konteynerin yazıcı ağına erişebildiğinden emin olun.
- **Hata yok ama çekmece açılmıyor**: kablo yazıcının çekmece çıkışında mı; çekmece kilitli (anahtar konumu) olmasın; yazıcı çekmece komutunu (ESC p) desteklemiyor veya farklı bir pin bekliyor olabilir (Star ve Xprinter modelleri cihazsız doğrulanamadı; bulgunuzu geliştiriciye bildirin).
- **Çekmece iki kez açılıyor**: yazıcının kendi ayarında "belge sonunda çekmece aç" seçeneği de açık olabilir; birini kapatın.
- **"Satış yok" düğmesi görünmüyor**: ayar kapalı, adres boş/hatalı veya kasiyerin "Çekmeceyi satışsız açma" yetkisi yok (Rol yönetiminden verilir); oturum başka kasiyere aitse de görünmez; kasa oturumu kapanmışsa "Satış yok" yalnız açık veya sayımdaki oturumda çalışır.
- **Nakit ödemede kasiyer 2 saniye bekliyor**: yazıcıya ulaşılamıyor; ödeme yine de alınır, adresi/ağı düzeltin.

Bilinen sınırlar: işletme için tek kasa yazıcısı tanımlanır (terminal başına ayrı çekmece yok); satışsız açmanın mevzuat yorumu mali müşavire sorulmalıdır.

### Waiter PWA (Android 14 tablet, Chrome, TalkBack)

- [ ] Installs to home screen from the manifest; launches full-screen.
- [ ] Pinch-zoom works (viewport no longer blocks it) — WCAG 1.4.4.
- [ ] TalkBack: header, status ribbon, zone nav, tables grid and the order
      drawer are all announced with meaningful names.
- [ ] Turn WiFi off mid-order: ribbon shows `Çevrimdışı • İşlemler Güvenli
      Kuyrukta`; turn WiFi on: the queued order reaches the server.
- [ ] Over plain `http://<lan-ip>` the app shows `Çevrimdışı mod kapalı` and does
      not pretend to queue.
- [ ] Target sizes ≥ 44 px; one-hand reachability for seat / add-item.

### PosTerminal (desktop, Chrome + Firefox + Edge, NVDA / VoiceOver)

- [ ] 200 % browser zoom: no horizontal scroll, no clipped controls — WCAG 1.4.10.
- [ ] 320 px width: shell is usable, context is a sheet.
- [ ] NVDA / VoiceOver: landmark navigation reaches every workspace; forms
      announce labels and errors.
- [ ] Keyboard only: full navigation, visible focus, no traps.

### DualScreen customer display

- [ ] Renders the order and running total at the customer-facing screen size.
- [ ] Recovers after a display reconnect / host restart.

Record the run (date, tester, device build, pass/fail per item) under
`evidence/` before flipping the go-live gate.

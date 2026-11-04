# V1-RMD-824 - Kasa sayfalarında 48 px dokunma hedefleri, ölü sekme stilleri ve dinamik durumlar için axe ve kontrast testleri

- Task ID: V1-RMD-824
- Status: Done
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin G8 grubunun ikinci parçası (`3-tasarim.md` T-12, T-13, T-14, T-16, T-17; `7-birlesik-dogrulama.md` satır 33; `2-arayuz-kodu.md` K11). PO kararı (2026-11-04): dokunma hedefleri en az 48 px; yönteminin yönü için doğru ARIA; boyut/CSS denetimi jsdom'da, dinamik durumlar için axe testleri. Kodda doğrulanan durum:

1. 48 px altı hedefler: `.cs-chip` (44 px, kasa hareketi gerekçe düğmeleri), `.cs-denom-count` (44 px, kupür sayımı girdisi), tahsilat sayfasının EFT onay kutusu (32 px kutu), `.modifier-option` (44 px, vanilla kasa seçenek satırı), `a.session-pill` bağlantıları (yüksekliği yalnız dolgudan gelir, ~28 px), React kabuğu arama kutusu (46 px). Düzeltme: hepsi en az 48 px.
2. `.sp-tab` / `.sp-tabs` stilleri tahsilat sayfasında hiçbir işaretleme tarafından kullanılmaz (ölü); yöntem seçimi `radiogroup`/`radio` (V1-RMD-362) olduğundan `role="tab"` yön seçimi kalmamıştır. Ölü stiller kaldırılır; kasa oturumu sayfasında `.cs-tab` stili ve kullanımı yoktur (doğrulandı).
3. Boyut denetimi yalnız PosTerminal CSS'ini kapsıyor (`touchTargets.test.ts`). Vanilla kasa sayfalarının CSS'i için aynı türden bir test yok: yeni StaticApps testi, kuralın açılışını okuyup bildirilen yükseklik/genişliğin 48 px altına inmediğini doğrular; arama kutusu mevcut PosTerminal testine eklenir.
4. Dinamik durumlar (tahsilat hazır, kasa kapalı bandı, yenileme uyarısı, kilitli mutabakat, açık iade bölümü, hata) için axe testi yok; kabuk testi yalnız boş `index.html`'i tarar. Yeni PosTerminal testi gerçek sayfa betiklerini jsdom'da çalıştırıp axe koşar.
5. Kontrast: jsdom renk hesaplayamadığından, kasa sayfalarının belirteç çiftleri (Kalan rengi, bildirim bantları, pasif çip, ipuçları, soluk metin) CSS'ten okunup WCAG oranı hesaplanır; 4,5:1 altı çift düzeltilir. Pasif düğmedeki `opacity` kontrastı düşürdüğü için belirteç renkleriyle değiştirilir.

Kapsam dışı: düşük kalemler, yazı tipleri, dist (V1-RMD-823).

## Owned surface

- `evidence/V1-RMD-824/**`
- `src/Clients/PosTerminal/src/dynamic-a11y.test.ts`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.css (V1-RMD-241) — çip ve kupür girdisi 48 px.
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css (V1-RMD-443) — onay kutusu, ölü sekme stilleri, pasif düğme kontrastı.
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js (V1-RMD-264) — eşit bölüştürme katlanır bölüme alınır: ödeme düğmesi ilk ekranda kalır.
  - tests/Clients/StaticApps/split-payment-hierarchy.test.js (V1-RMD-109) — bölüm testi.
  - tests/E2E/Cashier/specs/09-split-payment-race-and-edges.spec.js (V1-RMD-314) — eşit bölüştürme bölümünü açar.
  - src/Clients/Cashier/wwwroot/cashier-app.css (V1-CUI-008) — seçenek satırı ve oturum bağlantıları 48 px.
  - src/Clients/PosTerminal/src/styles.css (V1-RMD-042) — arama kutusu 48 px.
  - src/Clients/PosTerminal/src/touchTargets.test.ts (V1-RMD-699) — arama kutusu kuralı.
  - tests/Clients/StaticApps/cashier-touch-targets.test.js (V1-RMD-109) — yeni dosya: vanilla kasa sayfaları boyut testi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her kabul koşulu için önce kırmızı test, sonra düzeltme, sonra yeşil; mutasyon turu (`cp` + `cmp` ile geri alınır).
2. İngilizce kod ve test adları; ekrana basılan her metin Türkçe.
3. Görsel doğrulama: Playwright ile 360-768-1280 genişlikte dokunma ölçümü mümkünse; değilse jsdom/CSS ölçümü ve bu açıkça RESULT.md'de yazılır.

## Out of scope

- Sunucu, veritabanı, geçiş; sayfa düzeni (V1-RMD-822).

## Dependencies

- V1-RMD-822

## Acceptance evidence

1. StaticApps ve PosTerminal tam paket yeşil; oxlint ve tsc temiz; kırmızı sonra yeşil: her 48 px altı kural, ölü stillerin yokluğu, dinamik durumlarda axe ihlali yok, kontrast çiftleri ≥ 4,5:1.
2. Playwright E2E (kasa özellikleri) yeşil.
3. Mutasyon: bir kural 44 px'e indirilince, bir kontrast belirteci açılınca, bir durum bozulunca test kırılır; `cmp` ile geri alınır.
4. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. İki ardışık bağımsız qa-* incelemesi (qa-ux-tasarim, qa-frontend), bulgular giderilir, düzeltme sonrası doğrulama turu.
6. Semih'in elle deneyeceği senaryo: tablet boyutunda kasa hareketi gerekçe düğmeleri, kupür sayımı kutuları, EFT onay kutusu ve üst çubuktaki Kasa/Cari bağlantıları rahat parmakla basılır boydadır.

## Handoff

- None

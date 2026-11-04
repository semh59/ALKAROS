# V1-RMD-822 - Tahsilat sayfası hiyerarşisi: Kalan en büyük, "Ödemeyi Ekle" görünür, indirim/bahşiş/iade katlanır

- Task ID: V1-RMD-822
- Status: Done
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin G8 grubunun ilk parçası (`3-tasarim.md` T-02, T-03, T-25; `7-birlesik-dogrulama.md` satır 24). PO kararı (2026-11-04): tahsilat sayfasında Kalan, Toplam'dan büyük görünür; birincil "Ödemeyi Ekle" düğmesi indirim, bahşiş ve iade formlarının altında kaybolmaz; özet sırası tutarlı (Hesap toplamı, Ödenen, Kalan). Kodda doğrulanan kusurlar (`split-payment.js` `renderReady`):

1. Toplam 20 px ve Kalan 13 px yazılır; kasiyerin aradığı sayı (Kalan) en küçüğüdür. Düzeltme: Kalan büyük ve baskın (28 px), diğer iki satır küçük; etiketler "Hesap toplamı", "Ödenen", "Kalan".
2. Ödeme formu (yöntem, tutar, "Ödemeyi Ekle") sayfanın en altındadır; üstünde indirim, bahşiş, iade ve iptal formları durur, kasiyer her tahsilatta bunların altına kaydırır. Düzeltme: DOM sırası değişir (CSS `order` kullanılmaz): özet, uygulanan düzeltmeler ve yapılan tahsilatlar, eşit bölüştürme, ödeme formu ve "Ödemeyi Ekle"; ardından katlanır bölümler "İndirim ve bahşiş" ve "İade ve iptal".
3. Katlanır bölümler `<details>` olur; yenileme (5 sn yoklama ve her çizim) açık/kapalı durumunu bozmaz (durum `state` içinde tutulur); bir hata, bildirim ya da onay beklerken bölüm açık gelir (kasiyer sonucu görmeden kapalı kalmaz). Kilitli (mutabakat) durumda uyarı bandı yine en üsttedir.
4. Yapışkan (sticky) düğme kullanılmaz: 360 px'te girdileri örtmez.
5. Katlanma yüzünden Playwright adımları (18, 21, 25 numaralı özellikler) bölümü önce açar; ortak yardımcı `openFold`.

Kapsam dışı: dokunma hedefi boyutları ve axe (V1-RMD-824); düşük kalemler ve yazı tipleri (V1-RMD-823).

## Owned surface

- `evidence/V1-RMD-822/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js (V1-RMD-264) — özet, DOM sırası, katlanır bölümler.
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css (V1-RMD-443) — Kalan vurgusu, katlanır bölüm stili.
  - tests/Clients/StaticApps/split-payment-hierarchy.test.js (V1-RMD-109) — yeni dosya: özet ve sıra testleri.
  - tests/E2E/Cashier/lib/paymentHelpers.js (V1-RMD-264) — `openFold` yardımcısı.
  - tests/E2E/Cashier/specs/18-discount-and-tip.spec.js (V1-RMD-298) — bölümü açar.
  - tests/E2E/Cashier/specs/21-accessibility-keyboard-and-labels.spec.js (V1-RMD-356) — bölümü açar.
  - tests/E2E/Cashier/specs/25-split-payment-accessibility.spec.js (V1-RMD-362) — bölümü açar.
  - tests/E2E/Cashier/specs/14-till-queue-of-sent-checks.spec.js (V1-RMD-281) — geri gönder düğmesinin erişilebilir adı V1-RMD-821 ile değişti; arama kalıbı düzeltilir.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her kabul koşulu için önce kırmızı test, sonra düzeltme, sonra yeşil; mutasyon turu (`cp` + `cmp` ile geri alınır).
2. İngilizce kod ve test adları; ekrana basılan her metin Türkçe.
3. Görsel doğrulama: Playwright ile 360-768-1280 genişlikte tahsilat sayfası (yoksa jsdom ve CSS ölçümü; RESULT.md'de açıkça yazılır).

## Out of scope

- Sunucu, veritabanı, geçiş; React kabuğu; dokunma hedefi taraması.

## Dependencies

- V1-RMD-821

## Acceptance evidence

1. StaticApps tam paket ve ilgili Playwright özellikleri yeşil; kırmızı sonra yeşil: özet sırası ve etiketleri, Kalan'ın yazı boyutunun Toplam'dan büyük olması, "Ödemeyi Ekle"nin indirim/bahşiş/iade bölümlerinden önce gelmesi, bölümlerin varsayılan kapalı olması, yoklamadan sonra açık bölümün açık kalması, hata/bildirimde bölümün açık gelmesi, kilitli durumda uyarının ilk olması.
2. Mutasyon: her yeni davranış bozulunca ilgili test kırılır; `cmp` ile geri alınır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi (qa-frontend, qa-ux-tasarim), bulgular giderilir, düzeltme sonrası doğrulama turu.
5. Semih'in elle deneyeceği senaryo: bir hesabı tahsilata aç; en büyük sayı "Kalan"dır, hemen altında yöntem ve "Ödemeyi Ekle" görünür; "İndirim ve bahşiş" ile "İade ve iptal" kapalı başlıklardır, dokununca açılır.

## Handoff

- None

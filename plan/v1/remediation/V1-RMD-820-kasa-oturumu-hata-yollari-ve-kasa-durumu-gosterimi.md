# V1-RMD-820 - Kasa oturumu ve tahsilat hata yolları: kasa durumu okuma hatası "kasa kapalı" sayılmaz, EFT'ye kayma yok, React kabuğunda Kasa düğmesi ve bandı

- Task ID: V1-RMD-820
- Status: Done
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin G7 grubunun ilk parçası (`7-birlesik-dogrulama.md` satır 16, 25; `2-arayuz-kodu.md` K2, K4, K13; `3-tasarim.md` T-04, T-05; `9-1020-kasa-eslesme.md` T10 ve F0405, F0449). PO kararları (2026-11-04): React kasa ekranına üstte "Kasa: Açık/Kapalı" düğmesi ve kasa kapalıyken uyarı bandı; kasa kapalıyken tahsilatta yöntem sessizce EFT'ye kaymaz; kasa durumunu okuma hatası (500/403/429/503) "kasa kapalı" sayılmaz. Kodda doğrulanan kusurlar ve kapanışlar:

1. `cash-session.js` `loadActiveSession`: `/active` 404 ve 401 dışı her hata giriş formuna düşüyor (mesajsız), ağ hatasında aşama `checking` kalıp ekran "Oturum kontrol ediliyor…"da donuyor. Düzeltme: 401 giriş ekranı; diğer hatalar `loadError` aşaması ve duruma göre sabit Türkçe cümle; ağ hatası da `loadError` ve "Tekrar dene". (K2 a-b, satır 16.) Kasa Bilgisi Okunamadı ekranının stilsizliği (K12/T-01), taslakların korunması (K7), kapanışta "kapanmış olabilir" uyarısı ve beklenen tutar yokken pasif düğme (K8) güncel kodda zaten kapalıdır (V1-RMD-789/792); kanıt `evidence/V1-RMD-820/RESULT.md`'de.
2. `split-payment.js` kasa durumu üç değerli olur: açık, kapalı (404), okunamadı (diğer durum veya ağ hatası). Kapalıyken Nakit çipi pasif ve neden okunur bir bantta yazar ("Kasayı aç" bağlantısı kasa oturumu sayfasına); okunamadıyken ayrı uyarı ve "Tekrar dene"; 403'te yetki cümlesi. Hiçbir durumda yöntem kendiliğinden EFT'ye çevrilmez: yöntem seçilmemiş kalır, "Ödemeyi Ekle" pasif ve "Ödeme yöntemini seçin" ipucu gösterir. Pasif çipin kontrastı ≥ 4,5:1 (opaklık yerine belirteç renkleri). (K4, T-04, satır 16.)
3. `split-payment.js` yenileme: 5 sn'lik yoklama uçuştaki istek varken yeni istek açmaz; eski yanıt yeni sonucu ezmez (sıra sayacı); yenileme hata verirse ekranda "Hesap bilgisi güncellenemedi; tutarlar eski olabilir" uyarısı çıkar (F0405, F0449, K13 ikinci yarı). Odak geri yükleme, kimliksiz alanlar için (iade gerekçesi, ürün adedi, fiş no) kararlı bir özellikle çalışır (K13 birinci yarı).
4. PosTerminal: yeni `cash-session-status` özelliği. `cash.drawer` yetkisi olan oturumda satış ekranı başlığında ve `ExperiencePage` başlığında "Kasa: Açık" / "Kasa: Kapalı" / "Kasa: Bilinmiyor" düğmesi (≥ 48 px, `/cashier/payments/cash-session/index.html` bağlantısı); kasa kapalı veya okunamadıysa içerik üstünde uyarı bandı ("Kasa oturumu kapalı. Nakit tahsilat için kasayı açın." + bağlantı). Durum sekme odağa dönünce yeniden okunur (kasa başka sayfada açılır). 401'de oturum sonlandırma işleyicisi çalışır, 403/5xx "Bilinmiyor" olur, asla "Kapalı" denmez. (T-05, satır 25.)
5. Bu görevin yeni davranışı (yöntem seçili gelmez) mevcut StaticApps testlerini ve Playwright yardımcısını etkiler: kasa kapalı senaryoları EFT çipini açıkça seçer; `25-split-payment-accessibility` "varsayılan EFT" beklentisi "hiçbiri seçili değil" olur.

Kapsam dışı: React satış ekranı ilk dokunuş, bekleyen hesaplar ve cari hesaplar (V1-RMD-821); tahsilat sayfası yerleşimi (V1-RMD-822); düşük kalemler, yazı tipi, dist (V1-RMD-823); dokunma hedefi taraması ve axe (V1-RMD-824).

## Owned surface

- `evidence/V1-RMD-820/**`
- `src/Clients/PosTerminal/src/features/cash-session-status/CashSessionStatus.tsx`
- `src/Clients/PosTerminal/src/features/cash-session-status/cashSessionStatusApi.ts`
- `src/Clients/PosTerminal/src/features/cash-session-status/cash-session-status.css`
- `src/Clients/PosTerminal/src/features/cash-session-status/index.ts`
- `src/Clients/PosTerminal/src/features/cash-session-status/CashSessionStatus.test.tsx`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js (V1-RMD-241) — `/active` hata yolları.
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js (V1-RMD-264) — üç değerli kasa durumu, yenileme koruması.
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css (V1-RMD-443) — band ve pasif çip kontrastı.
  - src/Clients/PosTerminal/src/api.ts (V1-RMD-031) — kasa oturumu durumu okuması.
  - src/Clients/PosTerminal/src/strings.ts (V1-RMD-097) — Kasa düğmesi ve bant cümleleri.
  - src/Clients/PosTerminal/src/styles.css (V1-RMD-042) — başlıkta düğme yerleşimi.
  - src/Clients/PosTerminal/src/routes/Cashier.tsx (V1-RMD-097) — satış başlığında düğme ve bant.
  - src/Clients/PosTerminal/src/routes/workspace.tsx (V1-CUI-006) — ExperiencePage başlığında düğme ve bant.
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx (V1-CUI-006) — başlıkta düğme testi.
  - tests/Clients/StaticApps/split-payment-allowance.test.js (V1-RMD-521) — kasa kapalı senaryosu EFT'yi seçer.
  - tests/Clients/StaticApps/split-payment-change.test.js (V1-RMD-515) — kasa kapalı senaryosu EFT'yi seçer.
  - tests/Clients/StaticApps/split-payment-display-binding.test.js (V1-RMD-294) — kasa kapalı senaryosu.
  - tests/Clients/StaticApps/split-payment-failures.test.js (V1-RMD-264) — kasa kapalı senaryosu EFT'yi seçer.
  - tests/Clients/StaticApps/split-payment-item-refund.test.js (V1-RMD-525) — kasa kapalı senaryosu.
  - tests/Clients/StaticApps/split-payment-reasons.test.js (V1-RMD-514) — kasa kapalı senaryosu.
  - tests/Clients/StaticApps/split-payment-receipt.test.js (V1-RMD-517) — kasa kapalı senaryosu EFT'yi seçer.
  - tests/Clients/StaticApps/split-payment-refunds.test.js (V1-RMD-512) — kasa kapalı senaryosu.
  - tests/Clients/StaticApps/cashier-client-resilience.test.js (V1-RMD-264) — yenileme koruması etkisi.
  - tests/E2E/Cashier/lib/paymentHelpers.js (V1-RMD-264) — kasa kapalıyken yardımcı EFT'yi açıkça seçer.
  - tests/E2E/Cashier/specs/25-split-payment-accessibility.spec.js (V1-RMD-362) — varsayılan EFT beklentisi kalkar.
  - tests/Clients/StaticApps/cash-session-load-errors.test.js (V1-RMD-109) — yeni dosya: `/active` hata yolu testleri.
  - tests/Clients/StaticApps/split-payment-cash-state.test.js (V1-RMD-109) — yeni dosya: kasa durumu ve yöntem seçimi testleri.
  - tests/Clients/StaticApps/split-payment-refresh-guard.test.js (V1-RMD-109) — yeni dosya: yoklama ve eski yanıt koruması testleri.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her kabul koşulu için önce kırmızı test, sonra düzeltme, sonra yeşil; mutasyon turu (kaynak bozulunca ilgili test kırılır, `cp` + `cmp` ile geri alınır).
2. İngilizce kod ve test adları; ekrana basılan her metin Türkçe, hata metinleri durum koduna göre sabit; ham sunucu metni basılmaz.
3. `docs/UI_STYLE_GUIDE.md` §4 İngilizce sızıntı taraması dokunulan dosyalarda temiz.
4. Görsel doğrulama: Playwright ile 360-768-1280 genişlikte Kasa düğmesi/bandı ve tahsilat kasa-kapalı görünümü (yoksa vitest/jsdom).

## Out of scope

- Sunucu, veritabanı, geçiş; React satış ekranı sipariş akışı; tahsilat sayfası yerleşim sırası.

## Dependencies

- V1-RMD-795

## Acceptance evidence

1. StaticApps ve PosTerminal vitest tam paket yeşil; oxlint ve tsc temiz; kırmızı sonra yeşil: `/active` 500/403/ağ hatası giriş formuna düşmez ve donmaz; kasa kapalı/okunamadı ayrımı; yöntem seçilmemiş kalır; "Ödemeyi Ekle" pasif; "Kasayı aç" bağlantısı; yenileme koruması ve hata uyarısı; Kasa düğmesi üç durumda ve bant.
2. Mutasyon: üç değerli ayrım, EFT'ye kayma, bağlantı, yoklama koruması, sıra sayacı, düğme durumu bozulunca test kırılır; `cmp` ile geri alınır.
3. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
4. İki ardışık bağımsız qa-* incelemesi (qa-frontend, qa-ux-tasarim), bulgular giderilir, düzeltme sonrası doğrulama turu.
5. Semih'in elle deneyeceği senaryo: kasa oturumu kapalıyken bir hesabı tahsilata aç; Nakit pasif, bantta "Kasayı aç" bağlantısı görünür, hiçbir yöntem seçili gelmez; kasayı aç, geri dön, Nakit seçilebilir. Kasa başlığındaki düğme "Kasa: Kapalı"dan "Kasa: Açık"a döner.

## Handoff

- None

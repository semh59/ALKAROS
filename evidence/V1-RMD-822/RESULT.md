# V1-RMD-822 sonuç

Tahsilat sayfası: özet "Hesap toplamı / Ödenen / Kalan" sırasında, Kalan 28 px ve renkli (Toplam 20 px); ödeme formu ve "Ödemeyi Ekle" özetin hemen altında; "İndirim ve bahşiş" ile "İade ve iptal" katlanır `<details>` bölümleri formun altında (DOM sırası değişti, CSS `order` yok). Bölüm durumu `state.folds` içinde tutulur: yoklama ve yeniden çizim açık bölümü kapatmaz; hata, bildirim ya da onay bekleyen bölüm kendiliğinden açık gelir. Yapışkan düğme yok: 360 px'te girdileri örtmez. Kilitli durumda mutabakat uyarısı en üsttedir; kilitliyken indirim/bahşiş bölümü yoktur, iade bölümü vardır.

## Doğrulama
- Kırmızı: `split-payment-hierarchy.test.js` 7 testin 7'si düzeltmeden önce kırmızı, sonra yeşil.
- StaticApps: 290/292; kalan 2 kırmızı bu görevin dışında (G5: `error-dictionary-lock` İngilizce sunucu metni, `waiter-app` aralıklı zaman aşımı).
- Playwright E2E (gerçek Host + Postgres 55432): 07, 08, 09, 10, 11, 12, 14, 15, 16, 18, 21, 25, 29, 31 yeşil (14 ve 18 düzeltmeden sonra yeniden koşuldu). 14 numaralı özellik V1-RMD-821'in geri gönder düğmesi adı değişikliğiyle kırılmıştı; arama kalıbı düzeltildi (plan kapsam eki).
- Mutasyon: 7 mutant (etiket, Kalan boyutu, tercih hatırlama, zorla açma iki yönde, başlık, zorla açma koşulu), 7 öldürüldü, cmp ile geri alındı (mutation-summary.txt).
- Görsel: 360/768/1280 Playwright taraması canlı yığında bu çalışma ağacının imajı olmadığından yapılmadı; DOM sırası ve CSS bildirilen boyutlar jsdom testleriyle, gerçek tarayıcı akışları E2E ile doğrulandı. Genişlik taraması V1-RMD-824'te kasa sayfalarıyla birlikte yapılır.

## Bağımsız incelemeler (qa-frontend, qa-ux-tasarim)
Giderilen: bölüm durumu artık `toggle` olayına değil başlığa tıklamaya bağlı (yarış yok; kasiyerin kapattığı bölüm bir sonraki çizimde yeniden açılmaz, kendiliğinden açılan bölüm kalıcı olmaz); başlıkta açılma oku ve odak çerçevesi, başlık kimliği (odak yoklamada korunur); iade onayı ve bekleyen iade talepleri varken iade bölümü açık gelir; "Uygulanan tahsilatlar" ve düzeltme listesi formun altına taşındı, form özetin hemen altında; isteğe bağlı etiketlerin kontrastı.
Kabul edilen: kilitli durumda müdür formunun kasiyere de çizilmesi önceki davranıştır (yetki ayrımı sunucuda; ayrı iş); odak, kimliksiz iade düğmelerinde yoklamada kaybolabilir (görev öncesi de böyleydi, yoklama yalnız bekleyen kart onayında çalışır); "isteğe bağlı/opsiyonel" ayrımı V1-RMD-823'te.

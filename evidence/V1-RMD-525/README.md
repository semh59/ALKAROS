# V1-RMD-525 kanıtı

Eklenen: kasa sayfası ve PosTerminal iade panelinde "Ürün bazlı iade": kalem başına iade adedi (0 ile kalan adet), iadenin düşeceği ödeme (onaylı veya kısmen iade edilmiş), "Stoğa geri dönüş" için açık Evet/Hayır seçimi, gerekçe. Tutarı sunucu hesaplar ve iade sonrası gösterilir. Fiş ucu (`/receipt`) her kalemde `billItemId` ve `refundedQuantity` (reddedilmeyen isteklerdeki adet) döner.

Doğrulananlar (jsdom + enjekte edilmiş fetch; gerçek Host + Postgres yalnız fiş ucu için):
- Doğru gövde gönderilir (ödeme, gerekçe, kalem/adet, stoğa dön); dört parçadan biri eksikse düğme kapalı; kalandan fazla, sayı olmayan ve yalnız sıfır seçimi reddedilir; virgüllü adet kabul edilir; "Diğer" gerekçe metni gider.
- Açık isteği olan ödeme, iade listesi okunamaması, kalan adet kalmaması veya kalem listesinin okunamaması durumunda blok gösterilmez; tüm-ödeme iadesi çalışmaya devam eder.
- Bütçeden tamamlanan iade ayrı mesajla gösterilir; form temizlenir.
- Fiş ucu: kalem kimliği ve iade edilen adet; reddedilen sayılmaz, açık ve tamamlanan sayılır.
- Sonuç: PosTerminal tsc temiz, 66 dosya / 542 test, vite build 0; StaticApps 10 dosya / 83 test; fiş testi 4/4; tutarlılık denetimi temiz.

Doğrulanmayanlar: gerçek tarayıcıda görünüm. Önizleme tutarı yok; tutar sunucudan iade sonrası gösterilir.

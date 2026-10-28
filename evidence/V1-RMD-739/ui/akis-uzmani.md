# Kullanıcı gözü: akış ve hız uzmanı (kod okuma, çalıştırılmadı; müdür akışları incelenmedi)

Olumlu: garsonda masa aç + ürün ekle + mutfağa gönder 3 dokunuş; kasada bekleyen hesaptan tahsilata 2 dokunuş (split-payment.js:25,312).

Yüksek
- PT Cashier.tsx:692-698 - Her ürün dokunuşu sunucuya gidiyor, busy iken tüm ürünler pasif; yoğun saatte dokunuş kaybolur. Öneri: iyimser sepet.
- KitchenOperationsWorkspace.tsx:696 - İptal edilen kalem ekrandan sessizce siliniyor, "iptal edildi" uyarısı yok; aşçı hazırlamaya devam edebilir (bilet düzeyi süzme doğrulanamadı).

Orta
- PT Cashier.tsx:692,728,796 - Ürünler sipariş açılmadan pasif; her satışta "Yeni sipariş aç" ve "Sonraki siparişi aç" fazladan dokunuş.
- Cashier.tsx:693,505 - Not/seçenek yok; barkodla otomatik ekleme/Enter yok (eski kasada var, cashier-app.js:1013).
- Cashier.tsx:548-552 - React kasada vardiya aç/kapat bağlantısı yok (yalnız eski Cashier index.html:51).
- cash-session.js:755 - Kasaya dön bağlantısı yok.
- cashier-app.js:693-694,722,738,790,796 - Sabit KASA-1 masası, 3 ardışık istek, üçüncü hata yalnız konsola.
- split-payment.js:1574-1580 - Alınan para elle; hızlı tutar düğmesi yok.
- waiter-app.js:513 - Taslak satırı ±0.5 adım; 3 adet silmek 6 dokunuş.
- WA index.html:142-160 - Beş işlem yalnız ikon.
- Mutfak :739-757 - Kalem başına dört aşamalı çubuk, "tüm bilet hazır" yok (6 kalem = 12 dokunuş).
- Mutfak :535,661 - İptal gerekçesi elle yazılıyor, düğme yalnız "⚠".

Düşük: split-payment.js:1562-1568 indirim/bahşiş/eşit bölme formları hep açık; :300 kasa kapalı iken sessiz "Eft"; WA menu.js:72-73 "½" düğmesi aslında seçenek/not sayfası; void-comp.js:61-77 iptalde 3 dokunuş; Mutfak :445,503,544 "EXPO/ticket/Reprint" İngilizce sızıntı; :517-527 sağlık/yazıcı paneli aşçı ekranında yer kaplıyor.

# Kullanıcı gözü: pazar uzmanı (kod okuma, çalıştırılmadı; PT=PosTerminal/src, CA=Cashier/wwwroot, WA=WaiterPwa/wwwroot)

Yüksek
1. PT/routes/Cashier.tsx:693 - Hızlı satışta ürün seçeneği/modifiye ve satır notu gönderilmiyor (WaiterPwa ve Cashier vanilla'da var).
2. PT/routes/Cashier.tsx:761-767 - Kalem "Kaldır" tek dokunuşla siliyor; sebep ve yetkili onayı yok (WaiterPwa'da gerekçeli iptal/ikram var).
3. PT/routes/Cashier.tsx:781-788 - Hızlı satışta "Ödeme al" düğmesi yok; tahsilat dolambaçlı (workspace.tsx:443, pendingChecksApi.ts:44).
4. CA/index.html:125 - Satış ekranından ödemeye geçiş yok (yalnız "Siparişi onayla ve mutfağa ilet").
5. CA/payments/split-payment/split-payment.js:16 - Yemek kartı (Multinet/Sodexo/Ticket) ödeme yöntemi yok.
6. split-payment.js:1441,1498 - Çıktı mali olmayan "Bilgi Fişi" (window.print); yazarkasa/e-Arşiv akışı bu ekranda yok.
7. PT/features/management-closing/ClosingSection.tsx:57-58,118-119 - Gün sonunda müdür iptal ve yazdırma hata sayısını elle giriyor (varsayılan 0).

Orta
- split-payment.js:1566 - Tahsilat ekranında ürün bazlı bölme yok (ayrı ekranda, iki ekran arası gidiş).
- split-payment.js:918-931 - Bahşiş için %5/%10 hazır düğme yok.
- split-payment.js:300 - Kasa kapalıyken varsayılan yöntem sessizce "Eft".
- cash-session.js:710-729 - Kasa kapanışında X/Z raporu yazdırma ve yöntem kırılımı yok.
- WA/js/sheets/transfer.js:19-21 - Garson masa birleştiremiyor (PT'de var).
- WA/index.html:132-177 - Garson telefonunda hesap bölme, adisyon yazdırma, indirim yok.
- WA/index.html:158 - Kritik düğmeler yalnız ikon (kasaya gönder, masa değiştir, yardım, kişi sayısı).
- PT/routes/Cashier.tsx:523-567 - Paket satışında müşteri adı/telefon/adres alanı yok.

Düşük: PT Cashier.tsx:562 rol etiketi sabit "Kasiyer"; CA index.html:97 "İptal" aslında "Fişi Temizle"; PT Cashier.tsx:549-552 ve CA index.html:114 satışta masa seçimi yok (KASA-1 sabit).

Güçlü: kurs ateşleme, koltuk/kurs seçimi, çevrimdışı kuyruk, yardım çağır, kiosk PIN, gerekçeli iade/iptal, KDV dökümü, cari, kupür sayımı.
Hiç değinilmemiş: yemek kartı, karma ödeme etiketi, mali fiş, X/Z yazdırma, paket adres girişi, garsonda birleştirme, sadakat, hediye/zayi ayrımı, terazi.

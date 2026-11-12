# V1-RMD-1008 - Odemeyi Ekle ilk ekranda

Sonuc: kisa ekranlarda (max-height 860) hizli nakit tutarlari tek satir, tutar/kalan satirlari sikilastirildi. En kotu durum (masa adi + mutfaksiz not satiri var) gercek tarayicida (static sunucu + Playwright, API taklidi) Odemeyi Ekle dugmesinin alt kenari: 1366x768 ve 1024x768 = 738 px (once 794), 768x1024 = 966 px (ekran 1024). Goruntuler: odeme-*.png, onceki-1366x768.png.
Test: StaticApps 532/532 (static-all.log); yeni CSS yapi testi once kirmizi. Mutasyon 3/3 killed.
Inceleme sonrasi: 7 kolon yalniz genis (min-width 640) kisa ekranda, yazi 16 px, bosluk 8 px. Olcum: 1366x768 ve 1024x768 dugme alt kenari 742 px, 768x1024 966 px, 400x700 yatay tasma yok. Mutasyon 5/5 killed.

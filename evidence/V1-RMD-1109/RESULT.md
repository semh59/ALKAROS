# V1-RMD-1109 sonuc
Dogrulama (kod): bahsis tutari ve indirim degeri elle yaziliyordu, hazir yuzde yoktu. Alarm gercekti.
Duzeltme: split-payment.js presetButtons; bahsis %10/%15/%20 (tutar = hesap toplami - mevcut bahsisler, sunucu AdjustmentCalculator ile uyumlu), indirim %5/%10/%20 (tur Yuzde, deger yazilir, bekleyen onay durumu editedDiscount ile sifirlanir). Dugmeler yalniz formu doldurur, hicbir sey gondermez; "Bahsis ekle" ve "Indirim uygula", gerekce, tam kapatma teyidi ve yonetici onayi aynen kalir.
Kanit: red.log 8 kirmizi; green.log split-payment + cashier-ui 36 dosya 369/369; mutations.txt R1-R6 killed (otomatik gonderme mutantlari dahil); client-build-gate: yalniz vanilla Cashier.
Acik risk: kuruş yuvarlamasi kenar durumda 1 kurus sapabilir; sunucu yuzde indirimi toplam uzerinden, istemci onay kontrolu kalan uzerinden hesaplar (onceden de boyleydi); bekleyen onay varken yuzde dugmesi testi yok.

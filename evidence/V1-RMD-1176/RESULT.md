# V1-RMD-1176 sonuc
Yapildi: fis orijinal hakki sifirlama ucuna istege bagli ManagerUsername/ManagerPassword. Dolu ise arayan payments.take ister, yonetici InPlaceManagerApproval ile dogrulanir (payments.refund.approve, arayandan farkli, ayardan bagimsiz, 3 deneme/15 dk), 'orijinali basan sifirlayamaz' kurali yoneticiye uygulanir, denetim aktoru yonetici. Bos govde: eski yol aynen. Ekran: onay yetkisi olmayana 'Orijinal fisi sifirla (yonetici onayiyla)' karti (gerekce + yonetici adi + sifre type=password); sifre state'te tutulmaz. Eski istemci testi (yetkisize kart cizilmez) bilincli degistirildi: artik yonetici-onayli kart cizilir.
Celiski: yok.
Kanit: red.log 7/7, red-vitest.log 8 kirmizi, green.log BillReceiptHttpTests 49/49, green-vitest.log 21/21, arch-ApiConventions.log 17/17, mutations.txt (JS 3, sunucu 2 mutant olu).
Risk: kart basilmamis fis icinde de gorunur (sunucu 409 'fis basilmadi'); denetimde talep eden kasiyer yok; sayac bellekte; bazi hata yollari ayri test edilmedi.

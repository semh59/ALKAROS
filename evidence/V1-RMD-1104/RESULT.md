# V1-RMD-1104 sonuc
Dogrulama (kod): esit bolme yalniz kapali "Esit bolustur" bolumundeki kisi sayisi kutusu ve Hesapla ile yapiliyordu. Alarm gercekti.
Duzeltme: tutar alaninin altina "2 kisi / 3 kisi / 4 kisi" tuslari (data-split-people); tus kisi sayisini yazar ve mevcut submitEqualSplit'i cagirir (ayni kalan tutar, 50 kisi siniri, yuvarlama, Kisi N isareti, anahtar sifirlama). Mevcut bolum ve Hesapla korunur.
Kanit: red.log 5 kirmizi; green.log split-payment + cashier-ui + cash-session 39 dosya 479/479; mutations.txt R1-R4 killed; client-build-gate: yalniz vanilla Cashier, derleme gerektirmez.
Acik risk: kismen odenmis hesapta kalan tutar testi yok (ortak fonksiyon); dar ekranda form bir satir uzar, tarayicida olculmedi.

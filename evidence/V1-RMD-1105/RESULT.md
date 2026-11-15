# V1-RMD-1105 sonuc
Dogrulama (kod): tutar ve alinan para alanlarinda Enter dinleyicisi yoktu (yalniz musteri aramasinda vardi). Alarm gercekti.
Duzeltme: iki alana keydown Enter; submitTenderDisabled() ile ayni koruma (yontem, EFT onayi, cari musteri, kisa alinan para, gonderim suruyor) gecerse submitTender cagrilir; submitTender busy'yi senkron kurar, ikinci Enter tek istek gonderir.
Kanit: red.log 2 kirmizi; green.log split-payment + cashier-ui 34 dosya 356/356; mutations.txt R1-R4 killed; client-build-gate: yalniz vanilla Cashier.
Acik risk: kart ve cari yontemi icin Enter testi yok (ayni koruma ve ayni submitTender yolu).

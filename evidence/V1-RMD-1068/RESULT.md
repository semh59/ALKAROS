# V1-RMD-1068 RESULT

Tahsilat sayfasinda "Hesabi iptal et", "Iade iste", "Secilen urunleri iade et" ve "Onayli iadeyi iptal et" dugmeleri yeni .sp-btn-destructive sinifini alir: yuzey zemini, danger yazi, 1.5px danger kenar; pasifken mevcut pasif gorunum. Onay adimindaki dolgulu kirmizi onay (.sp-btn-danger) degismedi.
Iade onayi: iadeyi onayla ve onayli iadeyi iptal et onay dugmeleri zaten masa adi + tutari yaziyor (split-payment-refund-rules.test.js: "Evet, iadeyi onayla · Masa M-03 · ₺90,00"). "Iade iste" ise tek dokunusla istek gonderir ve onay adimi yoktur; ona onay adimi eklemek 6 test dosyasini ve akisi degistiren urun karari oldugu icin YAPILMADI (urun karari olarak listelenecek).
Kanit: red.log, green-file.log, green-all.log (StaticApps tum suite), mutations.txt (D1-D9, hepsi killed).
Olculemedi: gercek ekranda gorsel.
qa-frontend notu: Reddet ve "Kart cekilmedi olarak coz" tek dokunusla calisan nötr dugmeler olarak kaldi (kural 5/8 bunlari saymiyor); "Iade iste" tek dokunus yikici stil ile kural 5'in "tek dokunusla uygulanmaz" cumlesine takilir - urun karari olarak listelendi.

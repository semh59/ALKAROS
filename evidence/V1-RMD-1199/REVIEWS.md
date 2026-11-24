# V1-RMD-1199 qa
1. qa-ux-tasarim: uygulandi: liste `?billId=` ile istenir (200 kayit siniri asildi), ekran once acilir toplam sonra guncellenir (asili istek riski), liste okunamazsa toplam bilinmiyor notu. Birakildi: bicim farki ekranlar arasi, cok hesapli siparis toplami.
2. qa-test-kalitesi: uygulandi: liste hata yaniti ve tutarsiz yanit testleri (J5, J6, J8, J10 killed). Birakildi: ApprovedAmount null (Payment kurucusu Refunded icin yasaklar, esdeger), bayat yanit korumasi mutanti, 30 ms bekleme (mock mikro gorevde doner), PartiallyRefunded (risk RESULT'ta).

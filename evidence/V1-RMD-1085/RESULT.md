# V1-RMD-1085 sonuc
Bulgu dogrulandi (kod): arka uc EFT iade tamamlamada fiş numarasi istemez (PaymentRefunds.cs; istemci slipNumber:null gonderir), arayuzde dekont alani yok. Secilen yol: ALAN EKLENMEDI (sozlesme dekont alani tanimaz; uydurma alan yok), baslik gercege uyduruldu: yalniz EFT "havale gonderimi bekleniyor", karisik "fiş girişi veya havale gönderimi", yalniz kart "fiş girişi" (split-payment.js).
Yanlis alarm: "kart icin fiş numarasi gerekmez yazıyor": bu cumle yalniz Eft satirinda cikar (row.method === 'Eft'); kart satiri fiş alani ister. Degisiklik yok.
Etiket: urun iadesinde odeme secenegi "Ödeme · tutar" yerine Turkce yontem adi ("Nakit · ", "Havale / EFT · ", "Kart · "), yontem gelmezse "Ödeme".
Kanit: red.log (6 kirmizi), green-file.log 31/31, green-all.log StaticApps 616/616, client-build-gate.log exit 0, mutations.txt (5 killed, cmp ok).
Acik risk: arka uc yontemsiz/Nakit/Cari elle iade satirini da "fiş girişi" sayar (onceden vardi, kapsam disi).

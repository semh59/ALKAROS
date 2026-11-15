# V1-RMD-1108 sonuc
Dogrulama (kod): urun bazli iade formu odeme secimini her zaman bos aciyordu (tek odeme olsa bile). Alarm gercekti.
Duzeltme: split-payment.js selectedItemPayment: secilebilir (onayli/kismen iadeli, acik iade istegi olmayan) tek odeme varsa otomatik secili; birden cok odemede bos; secilen odeme listede degilse gecersiz sayilir. Stok Evet/Hayir secimi varsayilansiz kalir (dokunulmadi). Istek ayni requestRefundCall yolundan gider; onay ve kurallar sunucuda.
Mevcut split-payment-item-refund.test.js'te iki test eski davranisi (tek odemede de elle secme) kodladigi icin guncellendi: odeme adimi kaldirildi; iki odemeli kural yeni dosyada test edilir.
Kanit: red.log 2 kirmizi; green.log split-payment + cashier-ui 35 dosya 361/361; mutations.txt R1-R5 killed; client-build-gate: yalniz vanilla Cashier.
Acik risk: bayat secim dogrulamasi (odeme listeden cikarsa) kodda var ama ayri testi yok.

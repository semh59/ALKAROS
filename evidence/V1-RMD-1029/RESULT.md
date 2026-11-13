# V1-RMD-1029 - Masa hesabinda Kasaya don baglantisi masa planina gider

Duzeltme: split-payment.js loadBillItems, fis okununca masa varsa .sp-back baglantisini /tables, yoksa / yapar (statik varsayilan / kalir; fis okunamazsa eski davranis).
Kanit: yeni test once kirmizi; StaticApps split-payment* 24 dosya 268/268; mutasyon 2/2 killed. Istemci PosTerminal degismedi (client-build-gate gerekmez); Cashier istemcisinin package.json'u yok.
Gercek ekran: 8090 eski derleme; kod ve test kaniti (kullanici izniyle).
Acik (urun karari): baglanti metni "Kasaya don" kaliyor (hedef masa plani); odeme tamamlandi ekraninda ayri birincil dugme yok.

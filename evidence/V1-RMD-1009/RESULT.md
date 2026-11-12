# V1-RMD-1009 - Kisi basi tutar dugmesi odeme yontemini sifirlamaz

Kok neden: Kayitli dagilim (kisi basi) dugmeleri yontem dugmeleriyle ayni sinifi (sp-method-chip) tasidigindan yontem tiklama isleyicisi onlara da baglaniyor, data-method olmadigi icin secili yontemi bos yapiyordu ("Devam etmek icin bir yontem secin").
Duzeltme: yontem isleyicisi yalniz data-method tasiyan dugmelere baglanir (split-payment.js).
Kanit: yeni test (split-payment-person-method.test.js) once kirmizi; gercek tarayicida (Playwright) EFT secili iken Kisi 2 dugmesi tutari 125,00 doldurur ve EFT secili kalir (kisi-sonrasi-yontem-korundu.png). StaticApps 534/534. Mutasyon 2/2 killed.

# V1-RMD-1107 sonuc
Dogrulama (kod): satirdaki adet sabit metindi; 10 adet icin arti dugmesi 9 kez. Alarm gercekti.
Duzeltme: adet dugme oldu; dokununca sayi kutusu (inputMode decimal, virgul kabul) acilir; Enter veya odak kaybi mevcut api.changeQuantity ile uygular, Escape vazgecer; 0, negatif, gecersiz, ayni adet yok sayilir; gonderilmis siparişte dugme pasif. Enter + odak kaybi tek istek (ref). strings.ts quantityEntryText, styles.css .quantity-value/.quantity-input (mevcut --navy).
Kanit: red.log 10 kirmizi; green.log routes+features+shell+touch/contrast/text-size 111 dosya 1375/1375; tsc 0; mutations.txt R1-R7 killed; client-build-gate.log exit 0.
ATLANDI (karar): urun izgarasindaki disabled={busy} kilidi degistirilmedi; ardisik dokunus kuyrugu addingProduct ref'i, kitchenless yolu ve revizyon cakismasiyla etkilesir (yuksek risk, ayri gorev gerekir). Su an islem suruyorken urun dokunusu yok sayiliyor.
Acik risk: sunucu 999 ustunu ve gecersiz adedi reddeder ama istemcide ust sinir yok (100 yazilirsa uygulanir); kutu "1.5" noktali gosterir; kutu aciksa baska satira ilk dokunus kutuyu kapatir; gercek tarayicida blur gozlenmedi.

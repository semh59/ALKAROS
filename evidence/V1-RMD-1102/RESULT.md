# V1-RMD-1102 sonuc
Dogrulama (kod): "Kasayi ac" ve "Kasa oturumu" baglantilari duz adrese gidiyordu, donus yoktu; cash-session.js returnTo okumuyordu. Alarm gercekti.
Duzeltme: odeme sayfasi baglantiya tiklanirken ?returnTo=<kendi adresi> ekler (mevcut baglanti testi duz href bekledigi icin tiklama aninda; o test bu gorevin yuzeyi degil). cash-session.js returnTo'yu yalniz tek "/" ile baslayan, "//", ters bolu, bosluk ve kontrol karakteri icermeyen goreli yol ise kabul eder; gecerliyse geri baglantisi "Hesaba don" olur, kasa acilinca window.location.assign ile doner. Gecersizse yok sayilir.
Kanit: red.log 2 kirmizi; green.log cash-session + split-payment 37 dosya 468/468; mutations.txt R1-R5 killed; client-build-gate: yalniz vanilla Cashier, derleme gerektirmez.
Acik risk: orta/sag tik "yeni sekmede ac" click uretmez; o durumda donus olmaz (kasa yine acilir). Donus hedefi ayni kaynaktaki herhangi bir yol olabilir (sizinti degil, yalniz gezinme).

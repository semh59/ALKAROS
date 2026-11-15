# V1-RMD-1103 sonuc
Dogrulama (kod): masasiz duzenlenebilir siparis yalniz "Mutfaga gonder", gonderilince ayri "Tahsil et" sunuyordu. Alarm gercekti.
Duzeltme: Cashier.tsx sendAndCollect + "Gonder ve tahsil et" (strings.ts sendAndCollectLabel); masasiz, kalemli, bills.split yetkili siparisde birincil, "Mutfaga gonder" ikincil kalir. Gonderim basarisizsa (execute false: hata, 401, 409) odeme sayfasina gecilmez. Kasa kurali odeme sayfasinda kaldi, mutfaksiz akis degismedi.
Kanit: red.log 2 kirmizi; green.log Cashier/Table/workspace/Nfc 24 dosya 255/255 ve tsc; mutations.txt R1-R4 killed (R1 ilk denemede test bosluk cikardi, masa sepeti testi duzeltilip yeniden kosuldu); client-build-gate.log exit 0.
Acik risk: iki buyuk dugme alt alta (yanlis dokunus riski kucuk); mutfaksiz gerekce aktifken dugme gorunur ve yalniz gonderilmemis satirlari gonderir (test yok); 409/401 dali mutantla korunmuyor.

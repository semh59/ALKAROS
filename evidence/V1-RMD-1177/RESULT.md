# V1-RMD-1177 sonuc
Yapildi: odeme oncesi Hesap on fisi baslik "BILGI FISI", ibare "MALI DEGERI YOKTUR" (eski "Bu belge mali fis degildir." bunun icine alindi), numara "HESAP NO: N", ayri "Tarih" ve "Saat" satirlari (isletme saat dilimi). Sunucu degismedi (yalniz veri doner). Odenmis hesap fisi (split-payment.js) ayri, degismedi. docs/domain/bill-pre-receipt.md guncellendi, mali musavir teyit notu korundu.
Ad gerekcesi: "FIS NO" OKC fisindeki fis numarasiyla karisir; "Hesap fisi no" da "fis" icerir; "HESAP NO" adisyon/hesap numarasi gibi okunur, mali belge izlenimi vermez.
Kanit: red-vitest.log 4 kirmizi, green-vitest.log 125/125, mutations.txt (M1-M6 olduruldu, cmp RESTORED), client-build-gate.log exit 0, plan-audit 0/0, consistency clean.
Risk: "BILGI FISI" basliginin on hesap icin kullanimi dogrulanmadi (mali musavir teyidi bekliyor).

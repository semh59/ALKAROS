# V1-RMD-1119 sonuc
Kod dogrulama: split-payment.js'te iki /pending-checks geri baglantisi vardi (Hesap Bulunamadi ve odenmis/iptal ekrani BACK_LINK); tables ve Yeni satis baglantilari bilerek degismedi.
Duzeltme: ?from=recent-sales (tam esitlik, sabit href/label) ise baglanti "Son satislara dön" -> /recent-sales, alt metin "Son satislardan tekrar secin."; yoksa/baska deger ise eskisi aynen.
Kanit: red.log 2 kirmizi; green.log 8 yesil; green-full.log StaticApps 61 dosya/687 test yesil; mutations.txt M1 (param yok sayilir), M2 (her from), M3 (Hesap Bulunamadi sabit), M4 (BACK_LINK sabit), M5 (etiket degismez), M6 (alt metin degismez) killed (cp+cmp).
Acik risk: parametreyi gonderen ekran V1-RMD-1120 ile baglanir (o zamana dek degisiklik kullaniciya gorunmez); iptal edilmis hesap ekrani icin ayri test yok (ayni BACK_LINK).

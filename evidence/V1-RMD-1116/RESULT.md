# V1-RMD-1116 sonuc
Dogrulama (kod): cash-session.js returnTo gecerliyse etiketi kosulsuz "← Hesaba dön" yaziyordu; /tables'a giderken yanlisti: alarm gercek.
Duzeltme: returnTo yolu /tables (veya /tables?..., /tables#...) ise "← Masalara dön"; diger gecerli returnTo "← Hesaba dön"; returnTo yoksa/guvensizse "← Kasaya dön" (index.html'deki varsayilan, degismedi). /tables-report gibi benzer yol Masalar sayilmaz.
Kanit: red.log 3 kirmizi (kod stash ile eski haliyle); green.log 12 yesil; tum cash-session testleri green-full.log yesil; mutations.txt M1-M4 killed (cp+cmp).
Acik risk: yalniz /tables Masalar sayilir. Kasa kapaliyken baslik/bant baglantisi (V1-RMD-1114) returnTo'yu her calisma ekranindan (Bekleyen hesaplar, Mutfak vb.) gonderir; oralardan gelince etiket '← Hesaba dön' kalir (ayni yanilgi). Duzeltme Goal'u asar (genel etiket); ayri gorev gerekir.

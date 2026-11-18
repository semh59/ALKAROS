# V1-RMD-1154 sonuc

Metin gercek sizinti degil: "Bir gerekce secin; "Diger" icin not yazin" dogru Turkce, "not" kasa cekmecesi formundaki alan etiketi ("Not", cash-session.js:1081) ve istemci sozlugu ayni cumleyi tasiyor (cash-session.js:134, error-dictionary-lock ile kilitli). Metni degistirmek sunucu, istemci sozlugu ve testleri birlikte oynatirdi; yanlis pozitif veren siniflandirici duzeltildi: `not(?!\s+yaz)` istisnasi + teori testi (Turkce cumle gecer, "Do not retry." yakalanir).
Kanit: red.log (1 kirmizi), green.log (3/3), mut-M1 (istisna yok), M2 (not listeden dusmus), M3 (her not istisna) 3/3 olduruldu (kopyala-geri al-cmp). Tum proje PG gerektirdigi icin yalniz sinif kosuldu.
Kalan: istisna yalniz "not yaz..." kalibi; baska Turkce "not" kalibi eklenirse ayni yere kalip eklenir.

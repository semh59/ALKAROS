# V1-RMD-1121 sonuc
Dogrulama: sayim ekrani ve fark teyidi ekrani zaten kor (fark teyidine yalniz sayim gonderimi goturur); rapor 25 bu iki ekranda yanlis alarm. Gercek sizinti: nakit hareketi ozetindeki "Kasadaki beklenen nakit" satiri (sayimdan once tutari yaziyordu).
Duzeltme: satir kaldirildi (cash-session.js); cikis > cekmece korumasi ve sunucu kurali ayni.
Kanit: red.log 2 kirmizi, 1 yesil (sayim/kapanis ekranlari zaten dogru); green.log 3 yesil; green-full.log 11 dosya/180 test yesil; mutations.txt M1 (satir geri), M2 (koruma kaldirildi) killed (cp+cmp).
Acik risk: cikis korumasi "oracle" olabilir (farkli tutar deneyip beklenen nakdi kestirme); tamami sunucuya tasinmayi gerektirir, kapsam disi.

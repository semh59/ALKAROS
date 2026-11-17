# V1-RMD-1120 sonuc
Duzeltme: refundHref artik ?billId=..&open=refund&from=recent-sales uretir (recentSalesApi.ts); odeme sayfasi from'u V1-RMD-1119 ile tam esitlikle okur, geri baglanti "Son satislara dön" olur.
Kanit: red.log 1 kirmizi (from eksik); green.log 12 yesil; green-full.log 125 dosya/1490 test yesil; typecheck exit 0; client-build-gate exit 0; mutations.txt M1 (from yok), M2 (open=refund yok) killed (cp+cmp).
Acik risk: uctan uca (PosTerminal URL + sayfa etiketi) tek testte birlikte dogrulanmadi; iki taraf ayri testli (1119, 1120).

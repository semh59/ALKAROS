# V1-RMD-1012 - Masa sepetinde Hizli satisa gec dugmesi yok

Duzeltme: Cashier.tsx'ten dugme ve yalniz onun kullandigi leaveTableCart kaldirildi. Hizli satisa gecis Masalar ekranindaki Hizli satis dugmesiyle (temiz sepet, tableCart take-once) kalir.
Kanit: test once kirmizi (does not offer a quick-sale switch inside the table cart), sonra yesil; Cashier* 19 dosya 101/101; oxlint --max-warnings=0, tsc, vite build exit 0 (client-build-gate.log). Gercek tarayici (vite dev + canli API): masa M-01 Siparis al -> sepette dugme sayisi 0 (sepet-masa-hizli-satis-dugmesiz.png). Mutasyon 1/1 killed.

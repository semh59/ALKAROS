# V1-RMD-1180 sonuc
- Kirmizi: red-report.log (2 test), red-vitest.log (3 test). Yesil: green-report.log (21/21), green-vitest.log (81/81), client-build-gate.log exit 0.
- Mutantlar (mutants.txt, hepsi KILLED, cp+cmp ile geri alindi): R1 ExternalCard dali EFT'ye cevrildi; R2 MethodCount 4; R3 dal kosuluna AND false; C1 kapanis etiketi silindi; C2 iade ipucu kosulu kapatildi; C3 iade etiket sozlugu girdisi silindi; C4 son satislar etiketi silindi; C5 ExternalCard iade fisi alani gizlendi.
- Bilinen sinir: ayni odemede hem makbuz hem onayli kart denemesi olamaz (yalniz ExternalCardTenderStore makbuz yazar), bu yuzden dal sirasi icin ayri test yok.
- Etiket yazimi (Havale / EFT / EFT / Havale) onceden tutarsiz; kapsam disi. Kasiyer ekrani cipi V1-RMD-1181.

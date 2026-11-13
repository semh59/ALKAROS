# V1-RMD-1044 RESULT

Soru: kasiyer panelde kalem durumunu (Hazir/Servis edildi/Mutfaga gonderilmedi) gorebilsin mi (kasiyer denemesi 6, sorun 2). Veri var: GET /orders/table/{id} kalem basina kitchenState dondurur (OrderItemDto); panel "Mutfaga gonderilmedi" ve "Kasada eklendi" notunu zaten gosteriyordu. Yeni sunucu alani yok.
Duzeltme: TableOrderBridge lineNote: Draft -> "Mutfaga gonderilmedi", kasada eklenen -> "Kasada eklendi (mutfaga gitmedi)", aksi halde kitchenState Ready -> "Hazir", Served -> "Servis edildi" (metinler mutfak ekraninin itemStatusLabels'inden turetilir, Hazirlaniyor/Gonderildi gosterilmez). Mutfak durumu masa satirinin surumunu degistirmedigi icin kalem listesi panel acikken 15 sn'de bir sessiz yeniden okunur (arka plan hatasi listeyi bozmaz, panel kapaninca zamanlayici durur).
Kanit: red.log (not yok), red2.log (yeniden okuma yok); green PosTerminal tum suite 1333/1333; mutations.txt M1-M8 killed (M8 ilk turda hayatta kaldi, panel kapaninca okumayi durduran test eklendi); client-build-gate exit 0 (lint --max-warnings=0, tsc, vite build).
Acik: yoklama masa listesi gibi (8 sn) degil 15 sn; anlik itme (SignalR) yok, urun karari. Kasa HTTP yuku: acik panel basina 15 sn'de 1 okuma.
Elle senaryo: dolu masayi sec; mutfak ekraninda bir kalemi Hazir yap: en gec ~15 sn icinde panelde kalemin yaninda "Hazir"; Servis edildi yapinca "Servis edildi".

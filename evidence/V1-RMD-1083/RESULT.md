# V1-RMD-1083 sonuc
Bulgu dogrulandi (kod): "Hesap istedi" masasi bekleyen hesap listesinden turetiliyordu ama masa satirinda siparis/hesap kimligi yok; panel hasCheck=false oldugu icin Ode cizmiyor, "Sipariş al deyince" ve "uygun degil" yaziyordu.
Duzeltme: workspace.tsx bekleyen hesap listesinden tableId -> {orderId, itemCount, total} haritasi tutar (awaitingCheckTableIds ayni haritadan turer); TableOrderBridge awaitingCheck alir; masada kendi hesap/siparis yokken ve canCollect iken Ode (tek birincil), yon veren cumle, kalem/tutar satiri gosterir, yanlis "uygun degil" gerekcesini gostermez; Sipariş al ikincil.
Kanit: red.log (1 kirmizi), green-file.log 50/50, green-all.log PosTerminal 1412/1412, tsc 0, client-build-gate.log exit 0, mutations.txt (7 mutant, 7 killed, cmp ok), plan-audit.log, consistency.log.
Acik risk: ayni masada birden fazla bekleyen hesap varsa harita son kaydi tutar (onceki davranisla ayni kisit). Gercek tarayicida gorsel dogrulama yapilmadi.

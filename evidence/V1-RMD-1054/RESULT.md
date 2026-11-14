# V1-RMD-1054 RESULT
Yeni GET /api/v1/terminals/{id}/kitchen/.../print-jobs/failed?stationId= (RequireReadAsync, /tickets ile ayni yetki): istasyonun acik biletlerinin Failed/DeadLetter isleri tek SQL ile. Pano load() artik tek istek atar (30 acik biletle print-jobs istegi = 1, toplam 9; eskiden 30+).
Test: PosTerminal kitchen-operations 106/106; KitchenOperations C# 76/76 (tam kosu DESC siralama ve iptal-bilet eklenmeden once; sonra hedefli test gecti); ApiConventions 17/17; client-build-gate exit 0. 7 mutant killed, cmp ok.
Acik risk: LIMIT 500 (en yeni 500 doner, sessiz kesme, testsiz); ham SQL Host'tan (depo disi, sahiplik yuzunden); 403/yetkisiz-oturum testi yok (okuma izni gerekmez, /tickets ile ayni); gercek stack'te ag olcumu yapilmadi (Docker yok), kanit istek-sayisi testi.

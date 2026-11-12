# V1-RMD-1010 - Deneme 4 dogrulama maddeleri (kod degisikligi yok)

1. Ikram talebinin agda 4 kez gitmesi: gercek cift talep DEGIL. Limit ustu ikram yonetici onayi beklerken odeme ekrani, onay durumunu 5 saniyede bir ayni istek ve ayni islem anahtariyla yeniden gondererek yoklar (split-payment.js ensureApprovalPoll / probeApproval; ikram govdesi ayni imza = ayni anahtar). Sunucu bunu tek kayda indirir: BillAdjustmentGrantAuditHttpTests "A waiting request writes one pending record however often it is polled" ve es zamanli yoklama testi, 7/7 gecti (backend-grant-audit.log). Yonetici ekraninda tek talep gorunur.
2. "Hesabi al" metni: V1-RMD-1006 ile panel metinleri "Ode" diyor; kaynak taramasinda (src, test disi) "Hesabi al" kalmadi.
3. Mutfaga gonder (n): V1-RMD-1006 ile yalniz gonderilmemis kalem adedini gosterir (masa paneli ve satis ekrani).
Acik not: yoklamanin 5 sn araligi ve "Onay durumunu kontrol et" dugmesi urun karari; degistirilmedi.

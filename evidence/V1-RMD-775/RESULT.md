# V1-RMD-775 sonuc

Iddia dogrulandi: kabul siparis durumunu kilitten once okuyordu; iptal ve gonderme de kilitsiz okuyup tum baslik ve satirlari yaziyordu. Yaris iki yonlu gercekti (kabul iptali eziyor; iptal kabulu ve satir miktarini eziyor; gonderme iptali eziyor).
- red/, red2/: yeni yaris testleri duzeltmeden once KIRMIZI (exit=1).
- Duzeltme: LockAndAssertUnchangedAsync once siparis basligini FOR UPDATE ile kilitler ve durumu karsilastirir, sonra satirlari kilitler. Kabul, iptal ve gonderme ayni yontemi tek islemde kullanir (409 PurchaseOrderStatusException, mevcut eslemeyle).
- Son kosular: green3 (Purchasing.OrdersAndReceipts 31/31), host3 (3), apiconv3 (5), pinv (52), psup (29); hepsi exit=0. Digger klasorler (green2, host2, apiconv2, mut1-3, purch-*) ara kosulardir.
- mutations.log (m1-m3): durum karsilastirmasi kaldirilinca 3 test, kilit cagrisi kaldirilinca 4 test kirilir.
- Acik: baslik FOR UPDATE'i tek basina kaldirilinca (m2) hicbir test kirilmaz, cunku satir kilidi ayni bekletmeyi yapar. Baslik kilidi iptalle ayni kilit sirasini (kilitlenmeyi onleme) saglamak icindir; bu dogrudan testle kanitlanmadi.
- Yaris testleri kancali dekoratorle ardisik araya girmeyi dener; gercek es zamanli kosu ConcurrentReceiptsOfSameOrderPostStockOnce ve LockedOrderBlocksAnotherLockUntilTheFirstTransactionEnds testlerindedir.

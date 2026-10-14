# V1-RMD-529 kanıtı

- Ürün marj raporu `UnallocatedRefunds` taşır: aralıktaki Ödendi hesapların, kalem satırı olmayan tamamlanmış iade isteklerinin tutar toplamı. Ürün satırları ve `TotalNetRevenue` değişmedi.
- Bekleyen/reddedilen istekler ve kalemli iadeler bu toplama girmez.
- Test: ProductMargin 20/20 gerçek Postgres (`test-runs.log`); mutasyonlar `mutations.log`.
- Sınır: hesap tamamen iade edilip İptal olduysa hesap rapora hiç girmez, bu tutar da görünmez (ciro zaten yok).
- Doğrulanmadı: arayüzde gösterim yok; gerçek Host akışı denenmedi.

# V1-RMD-528 kanıtı

- Ürün marj raporu, tamamlanmış (`Completed`) kalem iadelerinin adedini ve satır net tutarını (satır net × iade adedi / satır adedi) düşer; bekleyen ve reddedilen istekler düşmez.
- Maliyet: aynı sipariş kaleminin, aynı stok kaleminin `Return` (kaynak Order) hareketleri tüketimden düşer; tüketim satırları önce (sipariş kalemi, stok kalemi) bazında toplanır.
- Test: ProductMargin 18/18 gerçek Postgres (`test-runs.log`); mutasyon 8/8 öldü (`mutations.log`).
- Sınır: kalem bilgisi olmayan kısmi iade kalemlere dağıtılamaz, bu rapor netlemez; hesap seviyesi indirim kalemlere dağıtılmaz.
- Doğrulanmadı: gerçek Host üzerinden uçtan uca iade → rapor akışı (testler tohumlanmış satırlarla çalışır).

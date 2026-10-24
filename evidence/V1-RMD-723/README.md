# V1-RMD-723 - Orders arka plan dayanıklılığı

**Sonuç:** B012, B015, B016 düzeltildi; B017 ve B011 sunucu yarısı kapsam dışı bırakıldı (gerekçe aşağıda).

- B012: mutfak olayı durumu geri sarmaz, Held kalemi "Queued" ile serbest kalmaz (`Order.AdvanceItemKitchenState`); atılan olaylar (bilinmeyen durum, eksik sipariş, eksik kalem) loglanır.
- B015: masa işaretçisi yalnız boşsa ya da gösterdiği sipariş Completed/Cancelled/Rejected ise alınır; pasif ve servis dışı masa yeni hesap almaz (409). QR/NFC onay bekleyen veya kabul edilmiş sipariş artık ezilmez.
- B016: süre dolumunda tek siparişin hatası loglanır, döngü kalan siparişlerle devam eder.
- Kapsam dışı: B017 (stok tüketiminin işlem bağlantısını kullanması) Inventory ve Recipes depolarında değişiklik ister; bu görevin yazılabilir yüzeyi dışında. B011 sunucu yarısı taslak kalem faturalama ürün kararına bağlı.

## Kanıt
- Mutasyon: `mutations.log` - 10 mutantın 10'u KILLED (ilk koşuda 3'ü derleme hatasıydı, mutant düzeltilip yeniden koşuldu).
- Testler: `logs/` (OrderAggregate 151/151, TableDraft CheckLifecycle 27/27, Confirmation expiry 6/6, mimari 17/17 ve 5/5).

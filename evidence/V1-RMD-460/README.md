# V1-RMD-460 kanıtı

- `online-menu-tests.log`: `ALKAROS.Host.Experience.OnlineOrdering.Tests` tamamı, 174 test geçti (yeni test `RejectedOrdersNameTheirUnmappedCodesPerPlatformUntilTheCodeIsMapped` dahil; gerçek Kestrel + PostgreSQL).
- `gercek-deneme.live.log`: gerçek Host + boş veritabanı + giriş yapılmış oturum. Reddedilmiş sipariş satırlarından Trendyol Go için `31` (2 sipariş) ve `32` (1 sipariş) döndü; 40 günlük `33` ve başka gerekçeli satırlar dışarıda kaldı; Yemeksepeti yalnız `SKU-9` gördü. `31` bir ürüne eşlenince (200) listeden düştü. Zorunlu seçimi olan ürün eşleme reddi (409) beklenen Türkçe mesajı verdi.

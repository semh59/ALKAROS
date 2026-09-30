# V1-RMD-465 kanıtı

- `reporting-host-tests.log`: `ALKAROS.Host.Experience.Reporting.Tests` 7/7 geçti (yeni test: gün sonu sayısı masa ve QR siparişini sayar, online siparişi saymaz).
- `mutation-online-counted.log`: `source <> 'Online'` süzgecini kaldıran mutasyon yeni testi kırdı (Expected: 2); kaynak geri alındı ve orijinalle aynı olduğu doğrulandı.
- `gercek-deneme.live.log`: gerçek Host + veritabanı. Bugüne 1 masa, 1 QR, 2 online sipariş kaydedildi; gün açılıp kapatılınca sipariş sayısı 2 (yalnız masa ve QR) oldu; istemcinin gönderdiği 999 yok sayıldı.

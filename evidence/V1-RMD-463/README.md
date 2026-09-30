# V1-RMD-463 kanıtı

- `reconciliation-host-tests.log`: `ALKAROS.Host.Experience.Reconciliation.Tests` 23/23 geçti (2 yeni test: zamanlanmış tur vaka üretir ve tekrar eden tur çoğaltmaz; başarısız tur bir sonraki turda yeniden denenir).
- `mutation-no-scan.log`: taramayı çağırmayan mutasyon iki testi de kırdı; kaynak geri alındı ve orijinalle aynı olduğu doğrulandı.
- `gercek-deneme.live.log`: gerçek Host + boş veritabanı. Reddedilmiş bir Yemeksepeti siparişi kaydedildi, tarama uç noktası hiç çağrılmadan yaklaşık 1 dakika sonra `OnlineOrderMismatch` vakası (Open) oluştu; Host günlüğünde `scan` çağrısı 0.

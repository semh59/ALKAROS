# V1-RMD-836 sonuç

Kapatılan: ikram uç noktası, `bills.comp` yetkisi olmayan kullanıcı için önce yetki talebini açıyor, kalemin değiştirilebilirlik kontrolünü sonra yapıyordu; garson kapanmış siparişin kalemi için ikram deneyince kişisel günlük bütçe ikram uygulanmadan harcanıyordu. Kontrol artık yetki talebinden önce (`OrderManagementEndpoints.cs`, yalnız yetkisiz dalda); kapsül içindeki kontrol aynı anahtarla tekrar gönderimi korumak için duruyor.

## Doğrulama
- Kırmızı (`red.log`): yeni testin ilk iki hâli (tamamlanmış sipariş, ödenmiş hesap) düştü; yetki kaydı sayısı 1 idi, 0 olmalı.
- Yeşil (`green.log`): Comp HTTP projesi 25/25.
- Mutasyon (`mutations.log`): kontrol kaldırıldı (4/4 test düştü), kontrol yalnız tamamlanmış siparişe daraltıldı (3 test düştü); ikisi de `cmp` ile geri doğrulandı.
- Atlanan test yok.

## Not
`void-sent` ucu da kalem kontrolünden önce yetki talebi açıyor; bütçe harcamadığı için bu görevin dışında, yalnız bekleyen kayıt bırakabilir (koordinatöre raporlandı).

## İncelemeler (qa-backend, qa-test-kalitesi)
İlk inceleme bulgu bulmadı (tekrar gönderimde kalem sonradan kapanmışsa 409 dönmesi kabul edilen davranış). İkinci inceleme iptal ve reddedilmiş sipariş durumlarının testsiz olduğunu ve `cmp` kanıtının logda yazılmadığını bildirdi: iki durum teste eklendi (25 test), mutasyon günlüğü `cmp` sonucunu yazıyor. Refused/Pending yollarının kapalı kalemle ayrıca sınanmaması kabul edildi: kontrol yetki çözümünden önce çalıştığı için bu yollara ulaşılamıyor.

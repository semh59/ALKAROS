# V1-RMD-552 kanıtı

Çöküş kanalı mantığı: sabit jetonlu bildirim (katman, kategori, zaman; serbest metin yok), 5 MB sınırlı yerel dosya tamponu, devre kesici, doğrudan kanal ve bekçi mantığı.

## Kabul koşulları (plan, daraltılmış)
1. Kanal kapalıyken kritik olay tampona düşer, devre kesici açılır, kanal dönünce tampon boşaltılır (ClosedChannelBuffersLocallyAndDrainsWhenItReturns, OpenBreakerSkipsTheNotifierUntilCoolDownPasses).
2. Ardışık 2 başarısız yoklamada tek alarm; sağlıklı yoklama alarmı sıfırlar (WatchdogRaisesOneAlarmAfterConsecutiveFailuresAndResetsOnRecovery).
3. Tampon dolunca en eski kayıt atılır ve sayılır (FullBufferDropsOldestAndCountsThem; sınır sınıf varsayılanı 5 MB, testte küçük sınırla doğrulandı).
4. Test: 4/4 geçti (`test-runs.log`); mutasyon 7/7 öldürüldü (`mutations.log`).

## Doğrulanamadı / devredilen
- Gerçek veritabanı veya API konteyneri durdurulup alarm süresi ölçülmedi; bekçinin ayrı konteynerde çalıştırılması ve gerçek bildirim kanalı bağlanmadı (V1-RMD-557).
- Mantık Host'a henüz hiçbir yerden çağrılmıyor; ErrorEventWriter başarısızlığını bu kanala bağlamak V1-RMD-538/557 ile yapılır.

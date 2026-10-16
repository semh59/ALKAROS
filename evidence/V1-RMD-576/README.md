# V1-RMD-576 - api imajına saat dilimi verisi

Değişiklik: `deploy/docker/Dockerfile` api aşaması paket satırına `tzdata` eklendi.

Kanıt (zone.log, build.log)
- `docker compose build api` çıkış kodu 0.
- Yeni imajla çalışan api kapsayıcısında `/usr/share/zoneinfo/Europe/Istanbul` var; `TZ=Europe/Istanbul date` +03 gösteriyor.
- Yeniden başlatmadan sonraki 2 dakikalık api günlüğünde `TimeZoneNotFoundException` yok.

Sınırlar (doğrulanamadı)
- Fatura taslağı akışı (`OnlineOrderInvoiceDrafting`) kapsayıcıda uçtan uca çalıştırılmadı; günlükte hata yokluğu, o türün çağrılmasını kanıtlamaz.
- Connector imajı bu görevin dışındadır.

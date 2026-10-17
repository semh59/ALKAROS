# V1-RMD-592 - Docker uçtan uca kanıt

Yığın (migrate, provision, api, web, watchdog) güncel koddan yeniden kuruldu (`compose-up.log`); veritabanı birimi korundu.

| Konu | Sonuç | Kanıt |
| --- | --- | --- |
| Göç 193 | Uygulandı (printers.profile) | docker-proof.log |
| Saklama temizliği | Eski satırlar silindi, yeniler kaldı; `monitoring.retention.run` kaydı health_checks 9352 satır silindi | docker-proof.log |
| Bekçi | Çalışıyor; kritik satırı üretiyor | docker-proof.log |
| Yazıcı ekle (ekran + API) | Genel IP 400 + Türkçe cümle, kayıt yok; 192.168.1.77 + Star kaydedildi (200, veritabanında profile=Star); düzenleyip Default yapınca profile boş (NULL) | screens.log, screen-1/2 |
| Eski ölçüm işareti | Ölçüm 10 dk eski gösterilince "Son ölçüm ... güncel olmayabilir" uyarısı çıktı (ağ yanıtı tarayıcıda değiştirilerek) | screens.log, screen-3 |

## Bulgular

- Planlı API yeniden başlatması bekçide `system.unreachable` alarmı üretiyor (2 başarısız yoklama ≈ 30 sn; yeniden başlatma bundan uzun sürdü). Beklenen davranış; bakım sırasında Telegram'a "kritik" gidebilir. Kusur olarak ele alınmadı, ayrı görev açılmadı.
- Stale işareti gerçek eski veriyle değil, tarayıcıda yanıt zamanı değiştirilerek kanıtlandı.
- Gerçek yazıcı ve gerçek Telegram ile doğrulama yok: doğrulanamadı.
- Test hesabı (kanit.592) işlem sonunda pasife alındı; test yazıcıları silindi.

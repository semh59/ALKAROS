# V1-RMD-545 kanıtı

Sentetik sağlık sondası: dakikada bir salt-okunur kontroller çalışır. Veritabanı sondası her zaman açık (tek `SELECT 1`); sağlık ucu sondası yalnız `Monitoring:Canary:BaseUrl` yapılandırıldığında eklenir. Oturum açılmaz, yazma yapılmaz. Başarısızlık Error (`canary.<ad>_failed`), 1 sn üstü yavaşlık Warning (`canary.<ad>_slow`) olay olur; olaylar `synthetic` işaretlidir.

## Kabul koşulları
1. Kalıcı sentetik kayıt sayısı 0: sondalar yalnız okur; gerçek veritabanı sondası çalışınca olay tablosuna satır eklenmez (RegisteredDatabaseProbeSucceedsAgainstARealDatabaseAndWritesNothing).
2. Sonda yükü: tur başına 1 sorgu (veritabanı sondası), dakikada 1 tur; sağlık ucu sondası bir HTTP isteğidir ve veritabanına ek sorgu atmaz (kodla sağlanır, ayrıca ölçülmedi).
3. Başarısızlık ve yavaşlık olay olur (FailingProbeRaisesAnErrorEventFlaggedSynthetic, SlowProbeIsAWarningAndFastProbeIsSilent).
4. Test 4/4, mutasyon 4/4 öldürüldü, DualScreenHostTests 6/6.

## Sınır
- Katalog okuma sondası ve gerçek giriş gerektirmeyen uçlar eklenmedi; sağlık ucu sondası BaseUrl ayarına bağlı (varsayılan kapalı). Canlı uygulamada bir tur gözlenmedi.

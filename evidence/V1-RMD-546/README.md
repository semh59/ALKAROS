# V1-RMD-546 kanıtı

Veritabanının kendi istatistik görünümlerinden (yalnız okuma): çalışan uzun sorgular, kilit zincirleri (bloklanan oturum ve bloklayanlar), bağlantı doluluğu, ölü satır oranı ve (eklenti varsa) `pg_stat_statements` ile en yavaş sorgular. Bulgular eşiklere göre hata olayına çevrilir (`db.lock_wait` Error; `db.long_running_query`, `db.connection_saturation`, `db.slow_statement`, `db.table_bloat` Warning).

## Kabul koşulları
1. Eklenti yokken hata vermeden "kapalı" (MissingStatementExtensionIsReportedAsOffWithoutError).
2. Yapay yavaş sorgu (`pg_sleep`) çalışırken listede görünür (ArtificialSlowQueryIsListedWhileRunning). `pg_stat_statements` listesi bu test veritabanında eklenti olmadığı için gerçek veriyle denenmedi (doğrulanamadı). "1 dakika" ölçüsü periyodik iş bağlanınca (Host) ölçülür.
3. Gerçek kilit zinciri: bekleyen oturum görünür ve Error olay üretir (LockChainShowsTheBlockedSessionAndRaisesAnErrorEvent). Eşikler testle sınandı.
4. 4/4 test gerçek Postgres'te geçti; mutasyon 6/6 öldürüldü.

## Sınır
- Henüz Host'ta periyodik çalışmıyor ve olayları yazıcıya vermiyor; bu bağlantı V1-RMD-558 zincirinde yapılır. Eklentinin açılması (yeniden başlatma gerektirir) dağıtım adımıdır, bu görevde yapılmadı.

# V1-RMD-532 kanıtı

- Önce: Docker test turunda `TheRestoreDrillRestoresARealShippedDump...` testi `pg_dump` sürüm uyuşmazlığıyla düştü (istemci 16.15, sunucu 18.6) — kayıt: önceki tam tur çıktısı.
- Değişiklik: `deploy/docker/Dockerfile` `test` aşamasında PGDG deposundan `postgresql-client-18`; üretim aşamaları değişmedi.
- Sonra: SecurityAdministration projesi Docker'da 29/29 geçti (`test-runs.log`).
- Mutasyon: Dockerfile için uygulanamaz; önce/sonra kanıtı yukarıdadır.

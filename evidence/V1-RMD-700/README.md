# V1-RMD-700 - Düşük yetkili veritabanı rolü ve veritabanı TLS

Kapanan bulgular: B036 (uygulama sahip rolüyle çalışıyordu), B077 (veritabanı bağlantısı TLS'siz, SSL kipi örtük).

## Ne değişti

- `deploy/docker/roles.sql`: `alkaros_app` (CRUD, DDL yok, göç geçmişine erişim yok) ve `alkaros_restore` (yalnız CREATEDB) rolleri; her `migrate` adımında göçten sonra yeniden uygulanır, parolalar `\getenv` ile dosyadan gelir.
- `compose.yaml`: api ve bağlayıcı `alkaros_app` ile çalışır, `cap_drop: [ALL]`, `no-new-privileges`. Sahip rolü yalnız migrate, provision ve operasyon servislerinde.
- `pg_hba.conf`: uzak bağlantılar yalnız `hostssl`, düz metin `hostnossl ... reject`. `postgresql.tuned.conf`: `ssl = on`, TLS 1.2 alt sınır. Sertifika anahtarı postgres kullanıcısına ait 0600 dosyaya kopyalanır.
- `DatabaseSslSettings`: tek SSL kipi kararı (api, tek seferlik komutlar, bağlayıcı, psql çocuk süreçleri). Ayar yoksa yalnız loopback düz metne düşebilir, diğer sunucular `Require`; compose `verify-full` verir. Geçersiz değer süreci durdurur.
- Geri yükleme provası kendi rolüyle (`ALKAROS_RESTORE_DB_USER` + parola dosyası) bağlanır.
- `generate-db-tls.sh`: CA + sunucu sertifikası (SAN postgres/localhost/127.0.0.1), mevcut anahtarın üzerine yazmayı reddeder.

## Kanıt

- Testler: `logs/targeted.log` 47/47; tam Host.Tests 315/315 (`logs/host-full.log`); ModuleBoundaries 11/11 ve ApiConventions 5/5 (`logs/arch-*.log`). DualScreen artık QrRelay'deki ortak SSL ayarına bağlı: onaylı bağımlılık listesi ve `module-dependency-rules.md` birlikte güncellendi.
- Gerçek PostgreSQL rol testleri (`DatabaseRolesTests`): bayraklar, CRUD serbest; CREATE DATABASE/ROLE/TABLE/SCHEMA, ALTER, DROP, TRUNCATE ve göç geçmişi reddedilir; sonradan eklenen tablo yeniden çalıştırmada yetkilenir; parola SCRAM saklanır.
- Gerçek TLS: `tls-trial.sh` + `logs/tls-trial.log`, tek kullanımlık konteynerde üretim entrypoint'i, conf ve pg_hba ile 18 kontrol PASS (anahtar 0600, verify-full şifreli, sslmode=disable reddedilir, yanlış CA reddedilir, roles.sql idempotent, yanlış parola reddedilir, parola dönüşümü).
- Mutasyon: `mutate.mjs`, `mutations.log` - 21 mutant, 21'i KILLED, kaynaklar geri yüklendi ve doğrulandı. (İlk koşu M10'da oturum yeniden başlatması yüzünden kesildi; kalan M10-M21 yeniden koşuldu.)

## Doğrulanamayan / açık riskler

- api ve bağlayıcı konteynerlerinin `cap_drop: ALL` altında fiilen başlaması denenmedi (canlı yığına dokunulamaz); yalnız `docker compose config -q` sözdizimi doğrulandı.
- Npgsql `Require` kipi sertifikayı doğrulamaz; ortam ayarsızsa uzak sunucuda yalnız şifreleme vardır. Compose `verify-full` ayarlar.
- `pg_restore` adımı PGSSLMODE/PGSSLROOTCERT ortamını devralır (Operations modülü kapsam dışı).
- Mevcut kurulumlarda `up` öncesi iki parola dosyası ve TLS dosyaları oluşturulmalı (README, kontrol listesi). Sertifika 825 gün geçerli; yenileme `ALKAROS_TLS_FORCE=1`.
- Sertifika/parola dosyaları yerelde üretildi, gitignore'da; içerikleri kayda geçmedi.

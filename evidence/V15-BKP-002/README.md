# V15-BKP-002 - Kanıt özeti

Isolated PostgreSQL restore verification: `V15-BKP-001`'in off-site
artifact'ini seçer, indirip şifresini çözer (corrupted/tampered artifact
burada, hiçbir veritabanı sağlanmadan reddedilir), gerçek bir tek-kullanımlık
Postgres veritabanına (`CREATE DATABASE`/`DROP DATABASE ... WITH (FORCE)`)
geri yükler, adlandırılmış bütünlük sorguları + bir uygulama başlatma dumanı
çalıştırır, süreyi `V0-BKP-002`'nin onaylı RTO eşiğiyle karşılaştırır ve
sonucu (başarı ya da başarısızlık) her zaman kaydeder.

## Dosyalar

- `build-release.txt` — tam solution Release build, 0 uyarı/0 hata.
- `test-restoreverification.txt` — yeni test projesi, 6/6.
- `test-regression.txt` — OffsiteBackup 24/24, Architecture.Tests
  (module boundary) 9/9, Host.Tests Manifest 17/17.
- `migration-139-verify.txt` — migration 139 gerçek Postgres 18'e
  (docker `alkaros-test-pg`) karşı ayrı bir scratch veritabanında
  ileri/geri doğrulaması.
- `plan-audit.txt`, `consistency-audit.txt`, `project-manifest.txt` —
  üç araç da temiz/VALID.

## Dürüst sınır

Restore edilen "yapı" bu görevde plaintext bir SQL script olarak modellendi
(doğrudan Npgsql üzerinden uygulanıyor), `backup.sh`'ın ürettiği `pg_dump`
custom-format (binary) artifact'lerle format olarak aynı değil — bu görev
restore ORKESTRASYONUNU (seçim/çöz/uygula/doğrula/kaydet) ve gerçek bir
izole Postgres'e karşı uçtan uca çalıştığını kanıtlıyor; gerçek artifact
formatı, o artifact'i üreten göreve/entegrasyona bırakıldı
(`V15-BKP-001`'in "yerel yedekleme oluşturma" kapsam dışı kararıyla aynı
gerekçe).

# V1-RMD-271 - Geri yükleme tatbikatı işi; gerçek yedek biçiminin (pg_dump custom) geri yüklenebilmesi ve parola düzeltmesi

- Task ID: V1-RMD-271
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

Erişilebilirlik envanteri: `V15-BKP-002` `RestoreVerificationOrchestrator` (en yeni uzak yedeği
çöz, yalıtılmış geçici veritabanına geri yükle, bütünlük ve duman kontrolü, RTO ölçümü, kayıt)
çağıransızdı. İşi bağlamak için gerçek bir yedekle çalıştırınca İKİ gerçek kusur çıktı:

1. **Biçim uyuşmazlığı.** Orkestratör yedeği UTF-8 SQL metni sanıp `ApplyScriptAsync` ile
   uyguluyordu; oysa `backup.sh` `pg_dump --format=custom` (ikili arşiv, `PGDMP` ile başlar)
   üretiyor. Gerçek bir yedek hiçbir zaman geri yüklenemezdi: tüm zincir (backup.sh → yükleme →
   tatbikat) yalnızca düz SQL metniyle hazırlanmış test verisinde çalışıyordu. Düzeltme:
   `IIsolatedRestoreDatabase.ApplyCustomFormatDumpAsync` (varsayılanı reddeder, sessizce geçmez);
   Npgsql gerçeklemesi `pg_restore` çalıştırır (bağımsız argüman listesi, parola yalnız alt sürecin
   ortamında, en fazla 500 karakter tanılama; Dockerfile zaten `postgresql-client` kuruyor,
   `ALKAROS_PG_RESTORE_PATH` ile yol verilebilir). Orkestratör `PGDMP` başlığını görünce bunu,
   yoksa eski SQL yolunu kullanır.
2. **Parola kaybı.** Modülün varsayılan bakım bağlantısı `NpgsqlDataSource.ConnectionString`
   idi; Npgsql bunu PAROLASIZ döndürür, yani geçici veritabanı oluşturma üretimde yetkilendirme
   hatasıyla düşerdi. Host artık tam bağlantı dizesini `HostDatabaseConnection` olarak sağlıyor ve
   fabrika onu (ya da açık `ALKAROS_RESTORE_MAINTENANCE_CONNECTION` değerini) kullanıyor.

Bakım işleri altyapısına (V1-RMD-268) `restore-verification` işi eklendi (günlük, elle de
çalıştırılabilir): `Settings` sınıfının en yeni uzak yedeğini doğrular. Yedek anahtarı başlatılmamışsa
ya da hiç uzak yedek yoksa `Skipped` + Türkçe neden bildirir. `GET .../backup/restore-attempts`
kayıtlı tatbikatları (başarı, süre, RTO içinde mi, kontrol sayısı, hata) listeler.

## Owned surface

- `plan/v1/remediation/V1-RMD-271-restore-drill-job-and-custom-dump-restore.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/Maintenance/RestoreVerificationMaintenanceJob.cs
  (V1-RMD-266 sahipliğindeki klasöre eklenen yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/HostDatabaseConnection.cs
  (aynı sahiplikte yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/SecurityAdministrationEndpoints.cs
  (yalnız iş kaydı, fabrika değiştirme, `restore-attempts` uç noktası ve DTO)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs
  (yalnız `HostDatabaseConnection` kaydı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Operations/RestoreVerification/IIsolatedRestoreDatabase.cs
  (V15-BKP-002 sahipliğinde kalır — yalnız yeni arayüz yöntemi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Operations/RestoreVerification/NpgsqlIsolatedRestoreDatabase.cs
  (aynı sahiplikte — yalnız `pg_restore` uygulaması)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Operations/RestoreVerification/RestoreExceptions.cs
  (aynı sahiplikte — yalnız yeni istisna)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Operations/RestoreVerification/RestoreVerificationOrchestrator.cs
  (aynı sahiplikte — yalnız biçim seçimi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/SecurityAdministration/SecurityAdministrationHttpTests.cs
  (2 yeni test, gerçek `pg_dump` yardımcısı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/SecurityAdministration/SecurityAdministrationTestDatabase.cs
  (yalnız geçici veritabanı sayacı)

## In scope

1. `restore-verification` işi, `restore-attempts` uç noktası.
2. Özel biçimli dökümü `pg_restore` ile geri yükleme; parola düzeltmesi.
3. GERÇEK `pg_dump` → şifrele → yükle → çöz → `pg_restore` → bütünlük kontrolü zinciri testi.

## Out of scope

- `Fiscal`/`OrdersInventory` sınıfları için geri yükleme (bunlar için WAL/temel yedek yok; bkz. V1-RMD-270).
- PITR tatbikatı (`restore-pitr.sh` ayrı bir kabuk akışı).
- Daha derin bütünlük kontrolleri (varsayılan iki kontrol: `identity.users`, `table_mgmt.tables`).
- Geri yükleme başarısızlığında otomatik uyarı.

## Dependencies

- V15-BKP-002
- V1-RMD-268
- V1-RMD-269
- V1-RMD-270

## Acceptance evidence

- Host.Experience.SecurityAdministration (UTF8 Postgres 18): 19/19. Yeni: anahtar/uzak yedek yokken iş
  `Skipped`; test veritabanının GERÇEK `pg_dump --format=custom` çıktısı yüklenir, tatbikat "2/2 kontrol
  geçti", kayıt başarılı ve RTO içinde, geçici veritabanı tatbikattan sonra SİLİNMİŞ; uzak kopyanın son baytı
  bozulunca tatbikat `Failed`, başarısız deneme kaydedilir, geçici veritabanı kalmaz.
- **Mutasyon kontrolü:** orkestratörde `pg_restore` dalı devre dışı bırakılınca gerçek-döküm testi
  `PostgresException` ile kırıldı (düz SQL yolu ikili arşivi çözemiyor); geri alınınca geçti.
- Değişiklikten etkilenen mevcut modül testleri geçmeye devam etti: yedek sağlığı 7/7, uzak yedek 24/24, geri yükleme doğrulaması 8/8.
- `python tools/plan-audit/plan_audit_tool.py validate`, `consistency_audit.py` çalıştırıldı.

## Handoff

- None

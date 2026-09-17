# V13-CSH-001 - Implement CashSession lifecycle

- Task ID: V13-CSH-001
- Status: Done
- Assignee: claude-code-session_01Xsqh6z1RYhmFapKkHKoBmk
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.38-I.44
- PDF:II.2.7
- PDF:II.5.9
- PDF:III.9

## Goal

Terminal/cashier bağlı Open, Counting, Closing, Closed ve Reconciled CashSession geçişlerini uygulamak.

## Owned surface

- `src/Modules/Cash/SessionLifecycle/**`, `tests/Modules/Cash/SessionLifecycle/**`,
  `database/migrations/V13/V13-CSH-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Kapsam genişletme onayı (2026-09-17 kullanıcı talimatı): bu task'ın yeni
  test projesinin `ALKAROS.slnx` ve `build/project-manifest.json` içine
  kaydı (V11-UNT-001 emsaliyle aynı desen).
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs,
  tests/Host/MigrationComposition/Manifest/ManifestTests.cs — yeni migration
  pozisyonunun kaydı (V11-INV-009/V1-RMD-230 emsaliyle aynı desen).
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Cash/ALKAROS.Cash.csproj
  (Cash modülünün kendisi sahipliğinde kalır) — yalnız Postgres erişimi için
  `Npgsql` paket referansı eklenir; mevcut ayarlar değişmez.
- Mekanik/otomatik yan etki (geri-tik olmadan, hiçbir dosya elle düzenlenmedi):
  Cash'e Npgsql eklenmesi, `dotnet restore --force-evaluate`'in Cash'e
  transitif olarak bağımlı olan tüm projelerin `packages.lock.json`
  dosyalarını (yaklaşık 31 dosya) otomatik yeniden üretmesini gerektirdi —
  build bu güncelleme olmadan derlenemiyor (NU1004). Tamamen deterministik,
  araç üretimli içerik; elle hiçbir bağımlılık eklenmedi/çıkarılmadı.

## In scope

- Tek açık oturum politikası, açılış bakiyesi, sayımlar, satır sürümü ve geçiş izinleri.
- PO:2026-09-16 kararı (en az iş ilkesi): aynı terminalin son `Closed`/
  `Reconciled` oturumunun kapanış anındaki gerçek (sayılmış) nakit tutarını
  döndüren salt-okunur bir sorgu (`GetSuggestedOpeningBalanceAsync` veya
  eşdeğeri) — `OpenSession` komutunun KENDİSİ bu öneriyi otoriter kabul
  etmez, yalnız UI'ın (V13-PUI-002) ön-doldurma için kullanacağı bilgi
  amaçlı bir okuma. Önceki oturum yoksa `null` döner (UI o zaman 0 gösterir).

## Out of scope

- Cash payment defter girişleri ve bildirim uyarıları.

## Dependencies

- V13-PAY-002
- V1-IAM-002
- V0-DOM-001
- V1-CSH-001

## Deliverables

- `src/Modules/Cash/SessionLifecycle/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Bir terminal ikinci bir çakışan oturumu açamaz; eski kapatma başarısız olur; Kapalı sessizce yeniden açılamaz.

## Handoff

- V13-CSH-002

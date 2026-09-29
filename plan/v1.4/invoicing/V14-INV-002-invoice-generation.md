# V14-INV-002 - Implement outgoing invoice generation

- Task ID: V14-INV-002
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.30-I.33
- PDF:II.2.17
- PDF:II.5.11
- PDF:III.20

## Goal

Onaylanmış GIB/QNB profili altında seçilen kaynak kümesinden invoice başlığını ve vergi gruplu satırları oluşturun.

## Owned surface

- `src/Modules/Invoicing/Generation/**`, `tests/Modules/Invoicing/Generation/**`,
  `database/migrations/V14/V14-INV-002/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Invoicing/ALKAROS.Invoicing.csproj — yalnız
  `ALKAROS.CustomerData` proje referansı (müşteri anlık görüntüsü şifreli profilden okunur)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/packages.lock.json, src/Modules/Invoicing/packages.lock.json ve
  tests/**/packages.lock.json — yalnız yeni proje referansının kilit dosyalarına yansıması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Modules/ModuleRegistry.cs,
  tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs ve
  tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs — yalnız yeni `Invoicing.Generation`
  modülünün kaydı, onaylı kenarları ve modül sayısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/module-dependency-rules.md — yalnız yeni modülün satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx — yalnız bu görevin test projesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs —
  yalnız bu görevin migration'ı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/unreachable_services_allowlist.json — yalnız bu
  görevin HTTP yüzeyi henüz olmayan tipleri
- Bu görev, başka bir task'ın owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- EFatura/EArşiv seçim girişi, UBL-gerekli tanımlayıcılar, vergi/yuvarlama, değişmez draft ve müşteri anlık görüntüsü.

## Out of scope

- QNB taşıma ve kayıtlı kullanıcı araması.

## Dependencies

- V14-INV-001
- V14-CST-001
- V0-CMP-001
- V0-CMP-002

## Deliverables

- `src/Modules/Invoicing/Generation/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Oluşturulan toplamlar, kaynak işlemler ve vergi gruplarıyla mutabakata varır; nesil hesap bakiyesine ikinci bir borç
  eklemez.

## Handoff

- V14-INV-003
- V14-QNB-002

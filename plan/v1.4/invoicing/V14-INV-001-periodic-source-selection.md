# V14-INV-001 - Implement periodic invoice source selection

- Task ID: V14-INV-001
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.30-I.33
- PDF:II.2.17
- PDF:II.5.11
- PDF:III.20

## Goal

Bakiyeyi değiştirmeden kapalı bir fatura dönemi için uygun faturalanmamış CustomerAccount işlemlerini seçin.

## Owned surface

- `src/Modules/Invoicing/SourceSelection/**`, `tests/Modules/Invoicing/SourceSelection/**`,
  `database/migrations/V14/V14-INV-001/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Invoicing/ALKAROS.Invoicing.csproj — yalnız
  `ALKAROS.CustomerAccounts` proje referansı (modül bağımlılık kuralları satır 18'in onaylı Customer Account kenarı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Modules/ModuleRegistry.cs,
  tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs ve
  tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs (V1-FND-001 sahipliğinde) — yalnız yeni
  `Invoicing.SourceSelection` modülünün kaydı, onaylı kenarları ve modül sayısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/module-dependency-rules.md (V0-ARC-001 sahipliğinde) —
  yalnız yeni modülün satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/packages.lock.json, src/Modules/Invoicing/packages.lock.json ve
  tests/**/packages.lock.json — yalnız yeni proje referansının kilit dosyalarına yansıması
  (`dotnet restore --force-evaluate`)
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx (V0-GOV-040 sahipliğinde) — yalnız bu görevin test projesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs —
  yalnız bu görevin migration'ı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/unreachable_services_allowlist.json (V1-RMD-272
  sahipliğinde) — yalnız bu görevin HTTP yüzeyi henüz olmayan tipleri
- Bu görev, başka bir task'ın owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Dönem sınırı, uygunluk, kilitleme, kaynağın benzersizliği ve yeniden çalıştırma davranışı.
- Fatura dönemi operator komutuyla kapatılır; kapalı döneme yeni kaynak eklenemez; kapanış tarihi kayıt altına alınır.

## Out of scope

- Invoice oluşturma/provider gönderimi ve gelen faturalar.

## Dependencies

- V14-ACC-002
- V14-ACC-003
- V0-DOM-007
- V0-CMP-002

## Deliverables

- `src/Modules/Invoicing/SourceSelection/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Bir işlem en fazla bir adet iptal edilmemiş invoice kaynak kümesine aittir; yeniden çalıştırma aynı kilitli seti
  döndürür veya çalışmaz.

## Handoff

- V14-INV-002
- V14-INV-003

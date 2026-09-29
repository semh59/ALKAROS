# V14-INV-003 - Implement invoice line to source traceability

- Task ID: V14-INV-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.30-I.33
- PDF:II.2.17
- PDF:II.5.11
- PDF:III.20

## Goal

Her invoice satırını, onu üreten tam hesap işlem kümesiyle eşleyin.

## Owned surface

- `src/Modules/Invoicing/SourceTraceability/**`, `tests/Modules/Invoicing/SourceTraceability/**`,
  `database/migrations/V14/V14-INV-003/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Modules/ModuleRegistry.cs,
  tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs ve
  tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs — yalnız yeni `Invoicing.SourceTraceability`
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

- Yazılan kaynak bağlantısı, satır/kaynak birleşimi, benzersizlik ve tutulan denetim yolu.

## Out of scope

- Invoice oluşturma ve provider status taşıma.

## Dependencies

- V14-INV-001
- V14-INV-002

## Deliverables

- `src/Modules/Invoicing/SourceTraceability/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Her satır bir veya daha fazla kaynağa uzanır; seçilen her kaynak temsil edilir; yetim ve yinelenen bağlantılar
  kısıtlamalarda başarısız olur.

## Handoff

- V14-QNB-002

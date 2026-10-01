# V15-KVK-002 - Implement cross-store anonymization

- Task ID: V15-KVK-002
- Status: InProgress
- Assignee: claude-code-session_01XpoF59o3sDPfb7ZADR4BMf
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.11-II.12
- PDF:III.33-III.34

## Goal

Onaylı PII anonymization işlemini idempotent, resumable ve store-checkpoint tabanlı workflow olarak uygulamak.

## Owned surface

- `src/Modules/Privacy/Anonymization/**`, `tests/Modules/Privacy/Anonymization/**`,
  `database/migrations/V15/V15-KVK-002/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/ALKAROS.Host.csproj — yalnız yeni proje referansı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/packages.lock.json ve tests/**/packages.lock.json — yalnız yeni proje
  referansının kilit dosyalarına yansıması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Modules/ModuleRegistry.cs,
  tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs ve
  tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs — yalnız yeni `Privacy.Anonymization`
  modülünün kaydı, onaylı kenarları ve modül sayısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/module-dependency-rules.md — yalnız yeni modülün satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx — yalnız bu görevin proje ve test projesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs —
  yalnız bu görevin migration'ı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/consistency_audit.py ve
  tools/consistency-audit/unreachable_services_allowlist.json — yalnız yeni modülün şeması ve HTTP yüzeyi henüz olmayan tipleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Program.cs — yalnız `kvkk-retention` komutunun bekleyen müşteri ve
  tedarikçi iş kalemlerini bu modülün akışına vermesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Program/KvkkRetentionTests.cs — yalnız komutun
  yeni çıktı alanları
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/compliance/kvkk-retention-runbook.md — yalnız yeni akışın anlatımı
- Bu görev, başka bir task'ın owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Per-field action, store checkpoint, retry/resume, referential integrity, audit entry ve final all-store verification.

## Out of scope

- Saklama planlaması ve şifreleme anahtarı rotasyonu.

## Dependencies

- V15-KVK-001
- V14-CST-002
- V15-SEC-003
- V1-OPS-001

## Deliverables

- `src/Modules/Privacy/Anonymization/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Seeded subject data her in-scope store checkpoint'inden sonra kaldırılır; interrupted workflow aynı noktadan güvenle
  devam eder; financial totals ve legal IDs geçerli kalır.

## Handoff

- V20-CMP-001

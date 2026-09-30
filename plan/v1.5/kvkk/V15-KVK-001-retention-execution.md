# V15-KVK-001 - Implement KVKK retention execution

- Task ID: V15-KVK-001
- Status: InProgress
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.11-II.12
- PDF:III.33-III.34

## Goal

Onaylanan veri envanterini değerlendirin ve tüm mağazalarda uygun silme/anonimleştirme işlemlerini planlayın.

## Owned surface

- `src/Modules/Privacy/RetentionExecution/**`, `tests/Modules/Privacy/RetentionExecution/**`,
  `database/migrations/V15/V15-KVK-001/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/ALKAROS.Host.csproj — yalnız yeni proje referansı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/packages.lock.json ve tests/**/packages.lock.json — yalnız yeni proje
  referansının kilit dosyalarına yansıması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Modules/ModuleRegistry.cs,
  tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs ve
  tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs — yalnız yeni `Privacy.RetentionExecution`
  modülünün kaydı, onaylı kenarları ve modül sayısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/module-dependency-rules.md — yalnız yeni modülün satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx — yalnız bu görevin proje ve test projesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs —
  yalnız bu görevin migration'ı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/consistency_audit.py ve
  tools/consistency-audit/unreachable_services_allowlist.json — yalnız yeni modülün şeması ve HTTP yüzeyi henüz olmayan tipleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Program.cs — yalnız `kvkk-retention` komutunun bu modülün politika ve
  seçimine bağlanması (iki ayrı seçim motoru kalmaz)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Program/KvkkRetentionTests.cs — yalnız komutun
  yeni çıktı alanları ve modüle bağlanması
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/compliance/kvkk-retention-runbook.md — yalnız yeni komut davranışı
- Bu görev, başka bir task'ın owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Politika sürümü, vade seçimi, yasal bekletme, prova, idempotency ve denetim.
- PO kararı (2026-09-30): 10 yıllık müşteri süresi son hesap hareketi ya da fatura tarihinden, tedarikçi süresi son sipariş
  tarihinden başlar; açık bakiyesi olan müşteri seçilmez. Diğer süreler onaylı envanterdeki gibidir (personel hesabı pasif
  olduktan 1 yıl, sipariş ve rezervasyon notları 5 yıl).
- Yürütme, vadesi gelen kayıtlar için değişmez bir iş kalemi (silme/anonimleştirme planı) üretir; alan düzeyinde uygulama
  V15-KVK-002'dedir. Mevcut `kvkk-retention` komutu bu seçime bağlanır.
- Bu görev iş kayıtlarındaki PII'nin saklama süresi sonunda silinmesi/anonimleştirilmesini kapsar; provider payload
  retention/silme V15-SEC-003 kapsamındadır; sınıflar çakışırsa V0-CMP-003 disposal matrisi üstündür.

## Out of scope

- Alan düzeyinde anonimleştirme uygulaması.

## Dependencies

- V0-CMP-003
- V14-CST-002
- V15-SEC-003

## Deliverables

- `src/Modules/Privacy/RetentionExecution/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Prova ve yürütme aynı uygun kayıtları seçer; yasal bekletme mutasyonu engeller; tekrarlanan çalışma stabildir.

## Handoff

- V15-KVK-002
- V20-CMP-001

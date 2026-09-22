# V15-SUP-001 - Implement redacted diagnostic bundle

- Task ID: V15-SUP-001
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.38
- PDF:I.42

## Goal

Gizli bilgileri, payment verilerini veya gereksiz kişisel verileri dışarı aktarmadan olayları teşhis eden sınırlı bir
destek paketi oluşturun.

## Owned surface

- `src/Modules/Support/DiagnosticBundle/**`, `tests/Modules/Support/DiagnosticBundle/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Bu, `Support` modülünün ilk (ve şimdilik tek) feature'ı: modül kök dosyaları
  (`SupportModule.cs`, `ALKAROS.Support.csproj`, `packages.lock.json`)
  `src/Modules/Support/DiagnosticBundle/**` deseninin dışında ama modülün ilk
  feature görevi kendi proje dosyasını sahiplenir konvansiyonuyla bu görevde
  oluşturuldu (C86/C88/C91 emsali).
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  ALKAROS.slnx (V1-FND-001 sahipliğinde), build/project-manifest.json
  (V1-FND-007 sahipliğinde) — yeni `ALKAROS.Support`/
  `ALKAROS.Support.DiagnosticBundle.Tests` proje kayıtları eklendi.
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  src/Host/ALKAROS.Host.csproj (V1-FND-001 sahipliğinde) — yeni
  `ALKAROS.Support.csproj` proje referansı eklendi.
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  src/Host/Composition/Modules/ModuleRegistry.cs (V1-FND-001 sahipliğinde) —
  `SupportModule` `DefaultCatalog`'a eklendi.
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  docs/architecture/module-dependency-rules.md, tests/Architecture/
  ModuleBoundaries/ModuleBoundaryTests.cs (V1-FND-013 sahipliğinde) — yeni
  satır 28 ("Support") ve `ApprovedEdges["Support"]` kaydı eklendi.
- Mekanik yan etki (elle düzenlenmedi): src/Host/ALKAROS.Host.csproj'a yeni
  proje referansı eklenince transitive kapanış değişti,
  `dotnet restore --force-evaluate` `ALKAROS.Host`'a bağımlı tüm test
  projelerinin `packages.lock.json`'ını yeniden üretti (FIND-IA-0043/
  V1-FND-001, V15-BKP-001 emsali).

## In scope

- Sistem durumu özeti, sürüm/yapılandırma parmak izleri, seçilen korelasyon günlükleri, redaksiyon, boyut/zaman
  sınırları ve paket denetimi.

## Out of scope

- Uzaktan kabuk erişimi, veritabanı dökümleri, otomatik harici yükleme ve olay çözümü.

## Dependencies

- V15-OBS-001
- V15-SEC-003
- V0-CMP-003

## Deliverables

- Yetkili tanılama paketi komutu/arabirimi.
- Gizli/PII sızıntısı, boyut sınırı, zaman penceresi ve eşzamanlı üretim testleri.

## Acceptance evidence

- `SeededSecretValueNeverAppearsInTheGeneratedBundle`: gerçek bir kart
  numarası audit event metadata'sına tohumlanır, üretilen paket bu değeri
  içermez (`evidence/V15-SUP-001/pytest...` değil, gerçek xUnit testi —
  bkz. `evidence/V15-SUP-001/test-results.txt`).
- `RevertAndConfirmWithoutTheValuePatternScannerTheSeededCardNumberWouldLeak`:
  revert-and-confirm — yalnız key-bazlı redaction hook kullanıldığında aynı
  değerin GERÇEKTEN sızdığı kanıtlanır, `SecretPatternScanner`'ın yük taşıdığı
  doğrulanır.
- `RecordsItsOwnGenerationOnTheAuditTrailForProvenance`: paket menşei
  (istekte bulunan aktör, sebep, bundle id) `IAuditEventStore`'a gerçek bir
  `support.diagnostic_bundle.generated` kaydı olarak yazılır ve geri okunur.
- `RejectsABundleThatWouldExceedTheSizeLimit`, `RejectsAWindowLargerThanTheMaximum`,
  `RejectsAnEmptyCorrelationIdSelection`: boyut/zaman/seçim sınırları gerçek
  testlerle doğrulandı.
- `GeneratesACorrectAndIndependentBundleUnderConcurrentRequests`: eşzamanlı
  üretim, çapraz-kirlenme olmadan doğrulandı.
- Toplam 19/19 yeni test (`ALKAROS.Support.DiagnosticBundle.Tests`, gerçek
  Postgres — migration 015 `audit.audit_events` + migration 028
  `observability.health_checks`), gerçek exit code ile geçti.
- `dotnet build ALKAROS.slnx -c Release` → 0 uyarı, 0 hata.
- `dotnet test tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj`
  → 9/9 (yeni `Support` modülünün `DependsOn`/`ApprovedEdges` tutarlılığı
  dahil).
- `dotnet test tests/Host/Experience/Composition/ALKAROS.Host.Experience.Composition.Tests.csproj`
  → 10/10 (yeni modül dahil tam DI graph gerçekten inşa ediliyor).
- Regresyon: `Security.SecretRotation` 28/28, `Observability.StructuredLogging`
  29/29, `Operations.OffsiteBackup` 24/24, `Host.Experience.Settings` 14/14.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `python tools/project-manifest/project_manifest_tool.py` → VALID.

## Handoff

- V20-DOC-002
- V20-GAT-002

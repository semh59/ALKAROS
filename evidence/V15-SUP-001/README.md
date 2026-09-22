# V15-SUP-001 - Kanıt özeti

Redakte edilmiş tanılama paketi (diagnostic bundle) — sistem durumu özeti,
sürüm/yapılandırma parmak izi, seçilen korelasyon-id'lerin denetim (audit)
kayıtları, iki katmanlı redaksiyon (key-bazlı `IRedactionHook` + değer-bazlı
`SecretPatternScanner`), boyut/zaman sınırları, kendi üretim menşeini
`IAuditEventStore`'a kaydeden bir paket üretim akışı.

Yeni bir `Support` modülü (`src/Modules/Support/`), ilk feature'ı
`DiagnosticBundle`. Kendi veritabanı şeması yok — mevcut Observability
(`IObservabilityService.GetUnhealthyChecksAsync`) ve Audit
(`IAuditEventStore.GetByCorrelationIdAsync`/`AppendAsync`) sınırlarını
kompoze ediyor.

## Yol boyunca bulunup düzeltilen gerçek kusur

`src/Host/ALKAROS.Host.csproj`'a yeni proje referansı eklenince
`RestoreLockedMode` tüm `ALKAROS.Host`'a bağımlı test projelerinin
`packages.lock.json`'ını reddetti — `dotnet restore --force-evaluate` ile
düzeltildi (V15-BKP-001 emsali, aynı sınıf).

Ayrıca yeni `SupportModule`'ü `ModuleRegistry.DefaultCatalog`'a eklemeden
önce mimari sınır testleri (`ALKAROS.Architecture.Tests`) yanlışlıkla
"geçiyor" görünüyordu — çünkü katalogda olmayan bir modül hiç
denetlenmiyor. Katalog kaydı + `ApprovedEdges["Support"]` + `module-
dependency-rules.md` satır 28 eklenince test gerçekten anlamlı hale geldi
(9/9).

## Dosyalar

- `build-release.txt` — tam solution Release build, 0/0.
- `test-results.txt` — `ALKAROS.Support.DiagnosticBundle.Tests`, 19/19.
- `architecture-tests.txt` — `ALKAROS.Architecture.Tests`, 9/9.
- `composition-tests.txt` — `ALKAROS.Host.Experience.Composition.Tests`
  (tam DI graph constructability, yeni modül dahil), 10/10.
- `plan-audit.txt`, `consistency-audit.txt`, `project-manifest.txt` — hepsi
  temiz/VALID.

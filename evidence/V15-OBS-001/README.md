# V15-OBS-001 - Kanıt özeti

Structured correlation logging: `src/Modules/Observability/StructuredLogging/**`.

- `LogSeverity`, `EventNameConvention` (dotted lowercase, örn. "order.accepted"),
  `IEventSampler`/`FixedWindowEventSampler` (sabit pencere içi tekrar sınırlama,
  ilk oluşumu asla düşürmez), `StructuredLogEvent`, `IStructuredEventLogger`/
  `StructuredEventLogger` (V1-OBS-001'in `CorrelationContext`'ini okur,
  `IRedactionHook` ile redakte eder, standart `ILogger` pipeline'ına
  `IReadOnlyList<KeyValuePair<string,object?>>` state olarak yazar — gerçek bir
  structured-logging sağlayıcısının alanları ayrı ayrı okuyabilmesi için).

## Dosyalar

- `build-release.txt` — tam solution Release build, 0 uyarı/0 hata.
- `test-structured-logging.txt` — yeni test projesi, 29/29.
- `test-foundation-regression.txt` — `ALKAROS.Observability.Foundation.Tests` regresyon, 22/22.
- `test-host-composition-regression.txt` — `ALKAROS.Host.Experience.Composition.Tests` regresyon (DI graph
  constructability), 10/10.
- `plan-audit-validate.txt`, `consistency-audit.txt`, `project-manifest.txt` — 0 hata/temiz/VALID.

# V15-OBS-001 - Implement structured correlation logging

- Task ID: V15-OBS-001
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.38-I.44
- PDF:II.2.25
- PDF:II.5.13
- PDF:III.28
- EXT:OWASP-LOGGING

## Goal

Critical flow'larda correlation, request, user/device ve provider reference alanlarını redaction kurallarıyla structured
log olarak yayınlamak.

## Owned surface

- `src/Modules/Observability/StructuredLogging/**`, `tests/Modules/Observability/StructuredLogging/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  src/Modules/Observability/ObservabilityModule.cs (V1-OBS-001 sahipliğinde) —
  yeni IEventSampler (FixedWindowEventSampler, 1s/20 event varsayılanı) ve
  IStructuredEventLogger (StructuredEventLogger) DI kayıtları eklendi;
  mevcut kayıtlar değişmedi.
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  ALKAROS.slnx (V1-FND-001 sahipliğinde) — yeni
  ALKAROS.Observability.StructuredLogging.Tests proje kaydı eklendi
  (C86/C88/C91 emsali).
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Bağlam yayılımı, event adlandırma, önem derecesi, örnekleme sınırı ve hassas alan filtreleri.

## Out of scope

- Metrik depolama, alert kuralları ve event kalıcılığını denetleme.

## Dependencies

- V15-SEC-003
- V1-OPS-001
- V1-OBS-001

## Deliverables

- `src/Modules/Observability/StructuredLogging/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Uçtan uca bir test, tek bir ID korelasyonu ile Order'den payment/mali'ye kadar izler ve hassas düz metin içermez —
  `StructuredEventLoggerTests.EmitCorrelatesOrderKitchenAndPaymentEventsUnderOneCorrelationIdWithoutLeakingPlaintextSecrets`:
  `CorrelationContext.BeginScope` altında order.accepted → kitchen.ticket.fired → payment.captured 3 event yayınlanıyor,
  üçünün de aynı `CorrelationId`'yi taşıdığı ve payment event'inin redakte payload'ında ham `cvv`/`auth_token`
  değerlerinin bulunmadığı (yalnız `ObservabilityRedactionHook.RedactedPlaceholder`) doğrulanıyor.
- `dotnet build ALKAROS.slnx -c Release` → 0 uyarı, 0 hata (`evidence/V15-OBS-001/build-release.txt`).
- `ALKAROS.Observability.StructuredLogging.Tests`: 29/29 (event adlandırma konvansiyonu, severity→LogLevel eşlemesi,
  sabit-pencere sampler'ın ilk oluşumu asla düşürmemesi + pencere içi limiti asla aşmaması + pencere sıfırlanması +
  event adına göre bağımsız izleme, korelasyon bağlamı yokken taze id üretimi, `IsEnabled` false iken sink'e hiç
  yazmama) — `evidence/V15-OBS-001/test-structured-logging.txt`.
- Regresyon: `ALKAROS.Observability.Foundation.Tests` 22/22, `ALKAROS.Host.Experience.Composition.Tests` 10/10 (DI graph
  constructability, V1-FND-013) — `evidence/V15-OBS-001/test-foundation-regression.txt`,
  `evidence/V15-OBS-001/test-host-composition-regression.txt`.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz (DB'ye yazmıyor, dosya/bellek tabanlı structured log —
  `MODULE_SCHEMA` sözlüğüne ek gerekmedi).
- `python tools/project-manifest/project_manifest_tool.py` → VALID.

## Handoff

- V15-OBS-002
- V15-OBS-003
- V20-GAT-002

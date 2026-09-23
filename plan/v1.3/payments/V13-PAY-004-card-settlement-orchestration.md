# V13-PAY-004 - Implement card settlement orchestration

- Task ID: V13-PAY-004
- Status: Done
- Assignee: claude-fork-v13-pay-004-2026-09-23
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.29
- PDF:I.49
- PDF:II.2.6
- PDF:II.2.16
- PDF:II.5.3-II.5.4
- PDF:III.8
- PDF:III.19

## Goal

Approved BankCard sonucu, PaymentAllocation ve fiscal request geçişini crash-safe durable workflow ile tamamlamak.

## Owned surface

- `src/Modules/Payments/CardSettlement/**`, `tests/Modules/Payments/CardSettlement/**`,
  `database/migrations/V13/V13-PAY-004/**`
- Bu görev Hugin transport, allocation constraint veya FiscalDocument schema'sını değiştiremez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/ALKAROS.Payments.csproj
  (yeni ALKAROS.Messaging ProjectReference eklendi), src/Host/Composition/Modules/ModuleRegistry.cs
  (CardSettlementModule kaydı eklendi), database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs (PhaseBMax 139→140),
  tests/Host/MigrationComposition/Manifest/ManifestTests.cs (RuntimeManifestIds/Count/
  LastEntryTables güncellendi), ALKAROS.slnx (yeni test projesi kaydı),
  tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs (ApprovedEdges'e
  `Payments.CardSettlement` satırı eklendi — module-dependency-rules.md'nin zaten
  kapsadığı Payment→Bill/Identity kaba kenarının aynısı, yeni doküman satırı gerekmedi),
  tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs
  (`DefaultCatalogContainsStandardProductionModules`'ın sabit modül sayısı 26→27)
  — hepsi paylaşılan dosyalar, plain text (backtick'siz).

## In scope

- Durable state, correlation, approved allocation, fiscal handoff, resume, duplicate suppression ve reconciliation lock.

## Out of scope

- Terminal protocol, refund, cash/meal-card handler ve reconciliation case persistence.

## Dependencies

- V13-HUG-001
- V13-ALC-001
- V13-FSC-001
- V1-FND-005
- V1-FND-006
- V1-SEC-002

## Deliverables

- `src/Modules/Payments/CardSettlement/**` altında durable orchestration production code'u ve migration'ı.
- Provider approval sonrası her crash noktası için failure-injection ve resume testleri.

## Acceptance evidence

**Gate note:** bu görevin `## Dependencies`'inde listelenen `V13-HUG-001` ve
`V13-FSC-001` gerçek kanıtla `Done` olmadan bu görev `Done` işaretlendi —
`V13-GOV-008` (2026-09-23, Semih onaylı) bu iki spesifik kenarı resmen
waive etti (`plan/GATES.md`'nin `V13_PAYMENT_ORCHESTRATION_DEPENDENCY_
WAIVER` tablosu, `plan_audit_tool.py`/`task_scope_tool.py`'nin eşleşen
sabitleri). Bu görev Hugin terminal protokolüne veya somut bir
FiscalDocument implementasyonuna hiç dokunmuyor — yalnız zaten Done olan
`ITenderHandler`/`TenderHandlerResult` sözleşmesi (V13-PAY-002) ve zaten
Done olan transactional outbox'ı (V1-FND-006) tüketiyor.

- **Durable state + correlation**: `payments.card_settlement_attempts`
  (migration 140) her denemeyi `idempotency_key` (UNIQUE) + kendi
  `provider_correlation_id`'siyle kalıcılaştırır.
- **Approved allocation**: `TenderApproved` → `Payment.Tender+Approve`,
  `PaymentAllocation` ve fiscal handoff outbox satırı TEK transaction'da
  commit olur (`CardSettlementOrchestrator.HandleAsync`).
- **Fiscal handoff**: `OutboxStore.EnqueueAsync` (V1-FND-006, dokunulmadı)
  ile aynı transaction'da `card-settlement.approved` event'i kuyruğa
  girer — gerçek bir Fiscal tüketicisi henüz yok (V13-FSC-001 hâlâ
  Blocked), bu görev yalnız durable kuyruklamayı sağlıyor.
- **Resume + duplicate suppression**: aynı `idempotency_key` ile
  tekrar çağrı (crash-and-resume simülasyonu) advisory lock altında
  var olan denemeyi bulup AYNEN replay eder — ikinci bir allocation veya
  outbox satırı asla üretilmez (`ResumeAfterACrashReplaysTheSameAllocation
  InsteadOfCreatingASecondOne`, `ConcurrentSubmitsOfTheSameAttemptProduce
  ExactlyOneAllocation`).
- **Allocation/provider mismatch**: aynı `idempotency_key` farklı
  `provider_correlation_id` VEYA farklı outcome ile gelirse
  `CardSettlementReplayMismatchException` fırlatılır, hiçbir şey
  değiştirilmez (`ADifferentProviderCorrelationUnderTheSameIdempotencyKey
  IsRejectedAsAMismatch`, `ADifferentOutcomeUnderTheSameIdempotencyKeyIs
  RejectedAsAMismatch`).
- **Reconciliation lock**: `TenderRequiresReconciliation` → `Payment.
  MarkUnknown` (Unknown'da durur, `RequestReconciliation`'ı ASLA
  çağırmaz) + kendi attempt satırı `RequiresReconciliation` outcome'unu
  sebebiyle kaydeder — bill kapanmaz, allocation/outbox hiç oluşmaz;
  bu satır `V13-REC-001`'in gelecekteki typed evidence'ıdır.
- **Declined**: `Payment.Decline`, allocation/outbox hiç oluşmaz.
- Judgement call: `TenderApproved.ApprovedAmount` hem `Payment.Tender`
  hem `Payment.Approve`'a aynı değer olarak geçiriliyor (ChangeAmount=0)
  — elektronik kart tahsilatında para üstü kavramı yok; kısmi
  otorizasyon (approved < attempted) PDF/task metninde tanımlanmamış,
  bu görev kapsamında ele alınmadı.

**Gerçek doğrulama (2026-09-23, Docker `alkaros-test-pg`, port 55432):**
- `dotnet build ALKAROS.slnx -c Debug` → 0 Uyarı, 0 Hata.
- `dotnet test tests/Modules/Payments/CardSettlement/ALKAROS.Payments.CardSettlement.Tests.csproj` → 8/8 başarılı (gerçek Postgres'e karşı: approved/declined/requires-reconciliation, resume, iki ayrı mismatch türü, gerçek eşzamanlı çift-gönderim, bilinmeyen bill reddi).
- Regresyon kontrolü: `ALKAROS.Payments.Allocations.Persistence.Tests` 13/13, `ALKAROS.Payments.PaymentAggregate.Tests` 43/43, `ALKAROS.Payments.TenderRouting.Tests` 28/28, `ALKAROS.Cash.TenderHandler.Tests` 6/6 — hepsi yeşil, sıfır regresyon.
- `dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj` → 161/161 (migration 140 dahil tam manifest/composition süiti). İlk koşuda 2 gerçek eksik bulundu ve düzeltildi: (1) `tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs`'nin `ApprovedEdges`'inde `Payments.CardSettlement` yoktu; (2) `HostModuleReachabilityTests.DefaultCatalogContainsStandardProductionModules`'ın sabit modül sayısı 26 kalmıştı. İkisi de düzeltildi, ikinci tam koşu 161/161.
- `python tools/project-manifest/project_manifest_tool.py` → `VALID (0 differences)`.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- Migration 140 ileri (`CREATE TABLE IF NOT EXISTS`/CHECK/FK) ve geri (`DROP TABLE IF EXISTS`) gerçek test veritabanında test fixture'ı aracılığıyla uygulanıp doğrulandı.

## Handoff

- V13-FSC-002
- V13-REC-001
- V13-TBL-001

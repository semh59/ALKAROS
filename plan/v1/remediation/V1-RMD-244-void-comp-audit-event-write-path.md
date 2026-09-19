# V1-RMD-244 - Wire real audit events into void/comp/void-sent

- Task ID: V1-RMD-244
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`V1-RMD-237` wired `IAuditEventStore`'un ilk gerçek çağrısını
`bills.discount`'a bağladı, ama kendi Out of scope'unda açıkça bıraktığı
`bills.void`/`bills.comp` (ve `V1-IAM-027`'nin eklediği `void-sent`)
`OrderManagementEndpoints.cs`'de — ayrı bir modül/dosya — hâlâ hiçbir
audit event yazmıyordu. `V1-RMD-116`'nın kendi yorumu ("the system-wide
audit trail crosses every module's aggregates (void/comp/discount
decisions included)") bu üçünü de zaten açıkça hedefliyor. Bu görev aynı
deseni buraya taşır.

## Owned surface

- `evidence/V1-RMD-244/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/OrderManagementEndpoints.cs
  (birikimli olarak birçok görevin sahipliğinde) — yalnız `/void`, `/comp`,
  `/void-sent` endpoint'lerinin başarı yolları, gerçek bir uygulama sonrası
  `IAuditEventStore.AppendAsync` çağrısı ekler; `IAuditEventStore` DI
  kaydı eklenir; grant `Pending`/`Refused` yolları veya başka hiçbir
  endpoint/davranış değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs,
  docs/architecture/module-dependency-rules.md (V1-RMD-215 sahipliğinde
  kalır) — `ALKAROS.Host.Experience.Orders`'ın onaylı edge listesine
  `Audit` eklendi; bu görevin kendi `IAuditEventStore.AppendAsync` çağrısı
  gerçek bir modül-sınırı testini (`HostOrchestrationEdgesStayWithinTheApprovedList`)
  bozuyordu, tam `dotnet test` koşusuyla yakalandı.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/Void/**,
  tests/Host/Experience/Orders/Comp/**, tests/Host/Experience/Orders/VoidSent/**
  (kendi görevlerinin sahipliğinde kalır) — her birine bir audit-event testi
  eklenir, mevcut testler değişmez. Bu doğrulama sırasında VoidSent'in kendi
  test projesinde, bu görevle TAMAMEN ilgisiz, önceden var olan ayrı bir
  fixture eksikliği bulundu: stok geri yükleme yolu (`PostgresStockItemRepository
  .GetByIdAsync`) migration 118'in (`reorder_point`) eklediği kolonu okuyor,
  ama `ALKAROS.Host.Experience.Orders.VoidSent.Tests.csproj` bu migration'ı
  hiç fixture'a bağlamamıştı (Purchasing/CashTenderHandler'da bulunanla aynı
  sınıftan bir bulgu, V1-RMD-239/241) — aynı diffte, gerçek koşuyla
  yakalanınca düzeltildi.

## In scope

- Başarıyla uygulanan bir void/comp/void-sent, `AggregateType="OrderItem"`,
  `AggregateId=itemId`, `ActorId=userId`, `Reason=request.ReasonCode`,
  `CorrelationId=context.TraceIdentifier` içeren gerçek bir `AuditEvent`
  yazar.

## Out of scope

- Grant-request (`Pending`/`Refused`) yollarının audit'lenmesi — zaten
  `identity.authorization_grants` tablosunda kayıtlı, yalnız fiilen
  UYGULANAN aksiyon audit'lenir (V1-RMD-237 ile aynı ilke).
- Diğer hassas komutlar (rol/yetki/ayar değişikliği vb.) — ayrı görevler.

## Dependencies

- V1-OPS-001
- V1-RMD-237

## Acceptance evidence

- Gerçek Postgres + gerçek Host'a karşı HTTP testi: void/comp/void-sent
  uygulandıktan sonra `IAuditEventStore.GetByAggregateAsync("OrderItem",
  itemId)` gerçek, doğru alanlı bir `AuditEvent` döner.
- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test` (Orders Void/Comp/VoidSent HTTP testleri) → yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None

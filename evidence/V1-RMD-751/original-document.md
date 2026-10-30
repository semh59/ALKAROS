# V1-RMD-237 - Wire a real audit event into the bill discount application path

- Task ID: V1-RMD-237
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`V1-OPS-001`'in kendi Goal'ı ("V1 critical command'ları için immutable
audit event üretmek") ve In scope'u ("temel V1 komut entegrasyonu") hiçbir
zaman teslim edilmedi: `IAuditEventStore.AppendAsync`/`AppendBatchAsync`
tüm `src/` ağacında SIFIR kez çağrılıyor (bağımsız bir denetim ajanının,
2026-09-18, tüm proje kod denetimi, bulgusu). Okuma tarafı gerçek ve doğru
yetkilendirilmiş (`KitchenOperationsEndpoints.cs`'nin `/audit/aggregate`,
`/audit/correlation` uçları, V1-RMD-116'nın kendi yorumuyla "void/comp/
discount decisions included" diye açıkça belirtiyor) — ama sorguladığı
tablo hiçbir zaman doldurulmuyor. Bu görev, üç eskalasyon-sınıfı komuttan
(`bills.discount`/`bills.void`/`bills.comp`) İLKİNİ (discount) gerçek bir
audit event üreten uca bağlar; diğer ikisi kasıtlı olarak Out of scope
(ayrı dosya/modül, ayrı görev — Bölme testi).

## Owned surface

- `evidence/V1-RMD-237/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/BillingSplitApplication.cs
  (birikimli olarak birçok görevin sahipliğinde) — yalnız `/discount`
  endpoint'inin başarı yolu, `BillingSplitStore.ApplyDiscountAsync`
  döndükten sonra gerçek bir `IAuditEventStore.AppendAsync` çağrısı
  ekler; `AddBillingSplitExperience`'a `IAuditEventStore` DI kaydı
  eklenir; başka hiçbir endpoint/davranış değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/BillingSplitHttpTests.cs
  (V1-RMD-040 sahipliğinde kalır) — yeni bir test eklenir, mevcut testler
  değişmez.

## In scope

- Başarıyla uygulanan bir indirim (`BillingSplitStore.ApplyDiscountAsync`
  başarıyla döndüğünde), `AggregateType="Bill"`, `AggregateId=billId`,
  `ActorId=principal.UserId`, `Reason=request.ReasonCode`,
  `CorrelationId=context.TraceIdentifier`, önce/sonra `payableAmount`
  içeren gerçek bir `AuditEvent` yazar.
- Bu event artık `GET .../audit/aggregate/Bill/{billId}` ve
  `GET .../audit/correlation/{correlationId}` ile gerçekten sorgulanabilir.

## Out of scope

- `bills.void`/`bills.comp` (ayrı dosya — `OrderManagementEndpoints.cs`,
  ayrı görev), kasa/ödeme/rol değişikliği gibi diğer hassas komutlar.
- Grant-request (`Pending`/`Refused`) yollarının audit'lenmesi — yalnız
  fiilen UYGULANAN (Applied) bir indirim audit'lenir; reddedilen/bekleyen
  talepler zaten `IDenialEventSink`/`identity.authorization_grants`
  tablosunda kayıtlı.

## Dependencies

- V1-OPS-001

## Acceptance evidence

- Gerçek Postgres + gerçek Host'a karşı HTTP testi: bir indirim
  uygulandıktan sonra `GET .../audit/aggregate/Bill/{billId}` gerçek,
  doğru alanlı bir `AuditEvent` döner (önceden boş dönüyordu).
- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test` (Billing HTTP testleri) → yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None

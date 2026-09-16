# V1-RMD-215 - Mimari sınır testi Orders/KitchenOperations kök isim alanlarını hiç görmüyordu

- Task ID: V1-RMD-215
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız çok-ajanlı denetimin (2026-09-16) **iki ayrı HIGH bulgusu**,
aynı kök nedeni paylaşıyor, tek görevde birleştirildi:
`ModuleBoundaryTests.HostOrchestrationEdgesStayWithinTheApprovedList`,
`ApprovedHostOrchestrationEdges` sözlüğünde yalnızca iki dar
alt-isim-alanını (`...Orders.OrderStockConsumption`,
`...Orders.SentItemVoid`) tarıyordu. Asıl Orders kompozisyon kökü
(`OrderManagementEndpoints.cs` ve kardeşleri) VE Kitchen'ın Host
orkestratörü (`ALKAROS.Host.Experience.KitchenOperations`, tamamı) bu
denetimden tamamen muaftı — biri yarın onaylanmamış bir modül
referansı eklese CI yeşil kalırdı.

## Owned surface

- tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs (ilgili
  test projesinin sahipliğinde)
- docs/architecture/module-dependency-rules.md (paylaşılan doküman,
  V1-RMD-159 emsaliyle aynı tabloyu güncelliyor)

## In scope

1. `ApprovedHostOrchestrationEdges`'e iki yeni, kasıtlı olarak GENİŞ
   kök girdi: `ALKAROS.Host.Experience.Orders` (Billing, Identity,
   Inventory, Kitchen, Orders, Settings) ve
   `ALKAROS.Host.Experience.KitchenOperations` (Audit, Identity,
   Kitchen, Operations, Orders, Settings). Her ikisi de `git grep`
   ile o ağacın altında gerçekten kullanılan her modülün birleşimi —
   iki mevcut dar girdinin (`OrderStockConsumption`, `SentItemVoid`)
   kendi listeleri bu birleşimin alt kümesi olduğu için, yeni kök
   girdi onları GEVŞETMİYOR, yalnız daha önce hiç denetlenmeyen kök
   dosyalara denetim ekliyor.
2. `docs/architecture/module-dependency-rules.md`'nin tablosuna aynı
   iki satır, V1-RMD-159 emsaliyle aynı formatta.

## Out of scope

- Diğer Host/Experience orkestratörleri (Billing, HelpRequests, vb.)
  — bu denetimin kapsamı yalnız Garson+Mutfak modülleriydi; başka bir
  alanda aynı boşluk varsa ayrı bir görev gerekir.

## Dependencies

- V1-RMD-159

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test tests/Architecture/ModuleBoundaries` → 9/9 yeşil;
  revert-and-confirm ile (onaylı listeden `ALKAROS.Kitchen` geçici
  olarak çıkarılıp) testin gerçekten 4 gerçek bağımlılığı
  (`OrderManagementEndpoints`, `OrderSubmissionCoordinator`,
  `SentItemVoidStore`, `PendingOrderConfirmationStore`) yakaladığı
  kanıtlandı.

## Handoff

- None

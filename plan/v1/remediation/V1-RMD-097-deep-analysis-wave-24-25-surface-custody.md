# V1-RMD-097 - Derin analiz wave 24-25 yüzey sahipliği

- Task ID: V1-RMD-097
- Status: Done
- Assignee: /root/rmd097_deep_analysis_surface_custody
- Work type: implementation
- Surface state: Existing

## Goal

`ALKAROS Derin Analiz` incelemesinin wave 24 ve wave 25 remediasyonlarında oluşturulan ama hiçbir plan
görevinin owned surface alanı tarafından kapsanmayan üretim ve test dosyalarını tek bir custody kaydına
bağlamak; böylece `plan_audit_tool.py validate` içindeki her üretim dosyası bir görev yüzeyine sahiptir
kuralı yeniden sağlanır ve bu dosyalar yalnızca yeni bir plan görevi ile değiştirilebilir hâle gelir.

## Owned surface

- `plan/v1/remediation/V1-RMD-097-deep-analysis-wave-24-25-surface-custody.md`
- `database/migrations/V1/V1-RMD-097/042-authorization-role-catalog.up.sql`
- `database/migrations/V1/V1-RMD-097/042-authorization-role-catalog.down.sql`
- `src/BuildingBlocks/IntegrationContracts/IIntegrationEventConsumer.cs`
- `src/BuildingBlocks/IntegrationContracts/IntegrationEventSerializer.cs`
- `src/BuildingBlocks/IntegrationContracts/TableIntegrationEvents.cs`
- `src/BuildingBlocks/Messaging/OutboxFanoutSink.cs`
- `src/Host/Outbox/OutboxComposition.cs`
- `src/Host/Outbox/OutboxDispatcherHostedService.cs`
- PO:2026-09-05 kararıyla src/Host/DualScreen/DualScreenApplication.Endpoints.cs yüzeyi RequireCashierPermissionAsync izin kodu yeniden eşleme için V1-IAM-024'e devredildi; bu historical task closed kalır.
- `src/Host/DualScreen/DualScreenExceptions.cs`
- `src/Host/DualScreen/DualScreenStore.Display.cs`
- `src/Host/DualScreen/DualScreenStore.Orders.cs`
- `src/Modules/Billing/Integration/TableEventBillConsumer.cs`
- `src/Modules/Orders/Integration/TableEventOrderConsumer.cs`
- `src/Clients/PosTerminal/src/format.ts`
- `src/Clients/PosTerminal/src/storage.ts`
- `src/Clients/PosTerminal/src/strings.ts`
- `src/Clients/PosTerminal/src/router.tsx`
- `src/Clients/PosTerminal/src/router.test.tsx`
- `src/Clients/PosTerminal/src/routes/Cashier.tsx`
- `src/Clients/PosTerminal/src/routes/CustomerDisplay.tsx`
- `src/Clients/PosTerminal/src/routes/workspace.tsx`
- `tests/BuildingBlocks/Idempotency/OutboxFanoutSinkTests.cs`
- `tests/BuildingBlocks/TestHelpers/OutboxTestDrain.cs`

## In scope

- Yukarıdaki dosyaların custody kaydını bu göreve almak ve `plan/AUDIT_MANIFEST.json` ile
  `plan/AUDIT_REPORT.md` bütünlük kayıtlarını mevcut ağaç durumuna göre yeniden üretmek.
- Wave 24'te yanlışlıkla `V1-RMD-090` klasörüne yazılan `042-authorization-role-catalog` migration
  dosyalarını, sahibi olan `V1-RMD-097` klasörüne taşımak; migration kimliği `042` ve
  `order.json` kaydı değişmez, discovery `rglob` tabanlı olduğu için davranış aynıdır.
- Kayıt, wave 24-25 remediasyonlarının davranışını değiştirmez; yalnızca yönetişim yüzey sahipliğini kapatır.

## Out of scope

- Sahiplenilen dosyaların içeriğini, wave 24-25 commitlerinde verilen hâlinden başka bir şekilde değiştirmek.
- Başka bir görevin owned surface alanını daraltmak veya `V1-RMD-090`, `V1-FND-002`, `V1-RMD-002` gibi
  tarihsel `Done` görevlerin metnini yeniden yazmak.
- Derin analiz kuyruğunda kalan `F-9` sonrası maddeleri veya yeni özellik davranışı üretmek.

## Dependencies

- V1-RMD-002

## Acceptance evidence

- `python -B tools/plan-audit/plan_audit_tool.py validate` çıktısında `UNOWNED_PRODUCTION_FILE` satırı
  bulunmaz ve komut exit code `0` verir.
- `python -B tools/plan-audit/plan_audit_tool.py verify-manifest` exit code `0` verir; manifest ve rapor
  dosya sayısı, satır ve bayt toplamları güncel ağaçla eşleşir.
- `dotnet build ALKAROS.slnx --configuration Release --no-restore` ve `pnpm --dir src/Clients/PosTerminal test`
  exit code `0` verir; sahiplenilen dosyalarda davranış değişikliği yoktur.
- Semih, bu görevin yalnız custody ve bütünlük kaydı eklediğini, sahiplenilen dosyaların diff'inin boş
  olduğunu doğrulayabilir.

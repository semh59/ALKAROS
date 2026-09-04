# V1-RMD-002 - Deep audit remediation

- Task ID: V1-RMD-002
- Status: Done
- Assignee: /root
- Work type: implementation
- Surface state: Existing

## Goal

PO:2026-08-24 kararıyla derin kod denetiminde doğrulanan build, composition, domain invariant, concurrency,
offline persistence, print recovery, query performance, browser security ve audit portability kusurlarını tek
konsolide remediasyon zincirinde kapatmak; gönderilen cashier siparişini station-scoped kitchen ticket'a aynı
transaction içinde projekte etmek.

## Owned surface

- `global.json`
- `tests/Host/MigrationComposition/packages.lock.json`
- `tests/Architecture/ModuleBoundaries/**`
- `src/Host/Composition/Modules/ModuleRegistry.cs`
- `src/Host/Composition/HostComposition.cs`
- `src/Modules/Audit/AuditModule.cs`
- `src/Modules/Cash/CashModule.cs`
- PO:2026-09-04 kararıyla src/Modules/Identity/IdentityModule.cs yüzeyi, yetkilendirme dalgasının DI kayıt evi olarak V1-IAM-019'a devredildi; bu historical task closed kalır.
- `src/Modules/Kitchen/KitchenModule.cs`
- `src/Modules/Observability/ObservabilityModule.cs`
- `src/Modules/Operations/OperationsModule.cs`
- `src/Modules/Reconciliation/ReconciliationModule.cs`
- `src/Modules/Reporting/ReportingModule.cs`
- `src/Modules/Settings/SettingsModule.cs`
- `tests/Host/MigrationComposition/Registry/ModuleRegistryTests.cs`
- `tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs`
- PO:2026-08-31 kararıyla src/Modules/Orders/SubmitOrder/**, src/Modules/Orders/OrderAggregate/**, tests/Modules/Orders/SubmitOrder/** ve tests/Modules/Orders/OrderAggregate/** yüzeyleri V1-RMD-064'e devredildi; bu historical task closed kalır.
- `src/Modules/Identity/DeviceSessions/DeviceSessionService.cs`
- `src/Modules/Identity/DeviceSessions/IDeviceSessionRepository.cs`
- `src/Modules/Identity/DeviceSessions/PostgresDeviceSessionRepository.cs`
- `tests/Modules/Identity/DeviceSessions/DeviceSessionServiceTests.cs`
- `src/Clients/WaiterPwa/SessionQueue/**`
- `tests/Clients/WaiterPwa/SessionQueue/**`
- PO:2026-08-24 UI yeniden tasarım kararıyla src/Clients/WebPrototype yüzeyi V1-RMD-003'e devredildi; bu
  historical task closed kalır.
- `src/Modules/Billing/BillFoundation/BillEnums.cs`
- `src/Modules/Billing/BillFoundation/BillMath.cs`
- `src/Modules/Billing/BillFoundation/BillSourceOperations.cs`
- `src/Modules/Billing/BillFoundation/IBillRepository.cs`
- `src/Modules/Billing/BillFoundation/PostgresBillRepository.cs`
- `tests/Modules/Billing/BillFoundation/ALKAROS.Billing.BillFoundation.Tests.csproj`
- `tests/Modules/Billing/BillFoundation/packages.lock.json`
- `tests/Modules/Billing/BillFoundation/Fixtures/**`
- `tests/Modules/Billing/BillFoundation/PostgresBillTests.cs`
- PO:2026-08-28 deep code audit kararıyla Bill.cs, BillItem.cs ve BillDomainTests.cs yüzeyi V1-RMD-035'e devredildi; bu historical task closed kalır.
- `src/Modules/Kitchen/PhysicalPrintRecovery/**`
- `tests/Modules/Kitchen/PhysicalPrintRecovery/**`
- PO:2026-08-31 kararıyla TicketLifecycle yüzeyi V1-RMD-062'ye devredildi.
- `database/migrations/V1/V1-RMD-002/**`
- `plan/PDF_SOURCE.md`
- `plan/AUDIT_REPORT.md`
- `plan/AUDIT_MANIFEST.json`
- `evidence/V1-RMD-002/**`
- PO:2026-08-24 production integration custody kararıyla tools/plan-audit/plan_audit_tool.py ve
  tests/Architecture/PlanAudit/test_plan_audit.py V1-FND-026'ya devredildi; bu historical task closed kalır.
- PO:2026-08-24 production dual-screen kararıyla src/Host/ALKAROS.Host.csproj, src/Host/packages.lock.json,
  database/MigrationComposition/order.json ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs
  V1-RMD-006'ya devredildi; bu historical task closed kalır.
- PO:2026-08-28 operational split kararıyla BillingModule.cs V1-RMD-027'ye devredildi; bu historical task closed
  kalır.

## In scope

- SubmitOrder ile KitchenTicket arasındaki transaction içi dispatch contract'ını kurmak; station-scoped ticket ve item
  graph'ının ilk submit'te atomik yazımını, idempotent replay ve rollback yollarını doğrulamak.

## Dependencies

- V1-FND-001
- V1-FND-025
- V1-ORD-004
- V1-IAM-015
- V1-WTR-004
- V1-BIL-004
- V1-KIT-004

## Acceptance evidence

- `dotnet restore ALKAROS.slnx`, `dotnet build ALKAROS.slnx --no-restore` ve ilgili bütün test projeleri exit 0 verir.
- Reconnect/revoke yarışı, restart sonrası offline kuyruk, negatif toplam reddi, stale print recovery, tek sorgulu aktif
  ticket yükleme ve DOM injection negatif yolları otomatik testlerle doğrulanır.
- İçinde en az bir active item olan cashier submit işlemi aynı transaction'da bir `Queued` kitchen ticket ve ticket item
  üretir; idempotent replay duplicate ticket üretmez, ticket insert hatası sipariş durumunu da rollback eder ve station
  scope'u konfigüre edilmemişse başarı fallback'i vermez. Gerçek PostgreSQL integration testi order→ticket graph'ını
  ve başarısız transaction yolunu doğrular.
- Yeni migration çiftleri boş PostgreSQL üzerinde forward/down/forward uygulanır ve manifest testi exit 0 verir.
- Plan audit doğrulaması makineye özel dosya yolu gerektirmeden çalışır; audit raporu ve manifest gerçek repository
  sayımlarıyla yeniden üretilir.
- `task_scope_tool.py --task-id V1-RMD-002` yalnız owned surface değişiklikleriyle exit 0 verir.

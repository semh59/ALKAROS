# V1-RMD-002 - Deep audit remediation

- Task ID: V1-RMD-002
- Status: Done
- Assignee: codex-root-v1-rmd-002
- Work type: implementation
- Surface state: Existing

## Goal

PO:2026-08-24 kararıyla derin kod denetiminde doğrulanan build, composition, domain invariant, concurrency,
offline persistence, print recovery, query performance, browser security ve audit portability kusurlarını tek
konsolide remediasyon zincirinde kapatmak.

## Owned surface

- `global.json`
- `src/Host/ALKAROS.Host.csproj`
- `src/Host/packages.lock.json`
- `tests/Host/MigrationComposition/packages.lock.json`
- `tests/Architecture/ModuleBoundaries/**`
- `src/Host/Composition/Modules/ModuleRegistry.cs`
- `src/Host/Composition/HostComposition.cs`
- `src/Modules/Audit/AuditModule.cs`
- `src/Modules/Cash/CashModule.cs`
- `src/Modules/Identity/IdentityModule.cs`
- `src/Modules/Kitchen/KitchenModule.cs`
- `src/Modules/Observability/ObservabilityModule.cs`
- `src/Modules/Operations/OperationsModule.cs`
- `src/Modules/Reconciliation/ReconciliationModule.cs`
- `src/Modules/Reporting/ReportingModule.cs`
- `src/Modules/Settings/SettingsModule.cs`
- `tests/Host/MigrationComposition/Registry/ModuleRegistryTests.cs`
- `tests/Host/MigrationComposition/Composition/HostModuleReachabilityTests.cs`
- `src/Modules/Orders/SubmitOrder/**`
- `src/Modules/Orders/OrderAggregate/**`
- `tests/Modules/Orders/SubmitOrder/**`
- `tests/Modules/Orders/OrderAggregate/**`
- `src/Modules/Identity/DeviceSessions/DeviceSessionService.cs`
- `src/Modules/Identity/DeviceSessions/IDeviceSessionRepository.cs`
- `src/Modules/Identity/DeviceSessions/PostgresDeviceSessionRepository.cs`
- `tests/Modules/Identity/DeviceSessions/DeviceSessionServiceTests.cs`
- `src/Clients/WaiterPwa/SessionQueue/**`
- `tests/Clients/WaiterPwa/SessionQueue/**`
- PO:2026-08-24 UI yeniden tasarım kararıyla src/Clients/WebPrototype yüzeyi V1-RMD-003'e devredildi; bu
  historical task closed kalır.
- `src/Modules/Billing/BillFoundation/**`
- `tests/Modules/Billing/BillFoundation/**`
- `src/Modules/Kitchen/PhysicalPrintRecovery/**`
- `tests/Modules/Kitchen/PhysicalPrintRecovery/**`
- `src/Modules/Kitchen/TicketLifecycle/**`
- `tests/Modules/Kitchen/TicketLifecycle/**`
- `database/migrations/V1/V1-RMD-002/**`
- `database/MigrationComposition/order.json`
- `tests/Host/MigrationComposition/Manifest/ManifestTests.cs`
- `plan/PDF_SOURCE.md`
- `plan/AUDIT_REPORT.md`
- `plan/AUDIT_MANIFEST.json`
- `evidence/V1-RMD-002/**`
- PO:2026-08-24 production integration custody kararıyla tools/plan-audit/plan_audit_tool.py ve
  tests/Architecture/PlanAudit/test_plan_audit.py V1-FND-026'ya devredildi; bu historical task closed kalır.

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
- Yeni migration çiftleri boş PostgreSQL üzerinde forward/down/forward uygulanır ve manifest testi exit 0 verir.
- Plan audit doğrulaması makineye özel dosya yolu gerektirmeden çalışır; audit raporu ve manifest gerçek repository
  sayımlarıyla yeniden üretilir.
- `task_scope_tool.py --task-id V1-RMD-002` yalnız owned surface değişiklikleriyle exit 0 verir.

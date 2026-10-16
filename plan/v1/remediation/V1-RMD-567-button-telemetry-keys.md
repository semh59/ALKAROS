# V1-RMD-567 - PosTerminal düğmelerine telemetri anahtarı

- Task ID: V1-RMD-567
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-16

## Goal

PosTerminal'deki her düğme sitesi `data-telemetry-id` taşır; etkileşim kaydı otomatik anahtara düşmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-567-button-telemetry-keys.md`
- `evidence/V1-RMD-567/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/authorization-decisions/AuthorizationDecisionsWorkspace.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/billing/RefundPanel.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/invoice-settings/SellerProfileCard.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-channel-report/ChannelReportSection.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-closing/ClosingSection.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-error-events/ErrorEventsSection.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-menus-recipe-cost/MenusSection.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-menus-recipe-cost/RecipeCostSection.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-order-invoices/OrderInvoicesSection.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-product-margin-report/ProductMarginSection.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-purchase-invoices/PurchaseInvoicesSection.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-purchasing-production/ProductionSection.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-purchasing-production/PurchasingSection.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-recipes/RecipesSection.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-security-system/DiagnosticBacklogPanels.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-security-system/MaintenanceBackupPanels.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-security-system/RecoveryPanel.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-security-system/SystemSection.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-settings-history/SettingsHistorySection.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-staff-roles/RolesPanels.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-staff-roles/StaffSection.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-stock/StockSection.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management/ManagementArea.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management/panel.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-hub/OnlineFoodHub.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-menu/OnlineMenuTab.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-operations/OnlineOperationsWorkspace.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-platform-credentials/OnlinePlatformCredentialsWorkspace.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-problems/OnlineProblemsTab.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/online-store-status/OnlineStoreStatusPanel.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/system-health/SystemHealthWorkspace.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/tables/FloorPlanWorkspace.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/tables/TableWorkspace.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/BusinessIdentitySettings.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/Cashier.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/CustomerDisplay.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/CustomerDisplayScreensaverSettings.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/NfcOrder.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/QnbCredentialSettings.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/RelaySettings.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/ReservationStation.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/SecurityAdministration.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/TokenTerminalSettings.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/workspace.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/shell/ProductionShell.tsx - yalnız düğmelere data-telemetry-id özniteliği
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/interaction-telemetry/buttonKeys.test.ts - yalnız anahtarsız düğme sınırı sıfıra iner
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Düğme sitesi taşıyan her kaynak dosyaya yalnız `data-telemetry-id` özniteliği eklenir (başlangıç commit'inde dosya listesi Sınırlı ek olarak yazılır); V1-RMD-541 statik taraması sıfır anahtarsız düğmeyle geçer.

## Out of scope

- Düğme davranışı, metni ve görünümü değişmez.

## Dependencies

- V1-RMD-541

## Acceptance evidence

1. Statik tarama anahtarsız düğme sitesi sayısını 0 bildirir.
2. Mevcut ekran testleri değişmeden geçer.
3. Çıktılar `evidence/V1-RMD-567/` altındadır.

## Handoff

- None

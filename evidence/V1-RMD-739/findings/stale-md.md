# Ölü yola atıf yapan görev md dosyaları
Toplam: 80
plan/v1/billing/V1-BIL-002-split-design-persistence.md | Done | src/Modules/Billing/SplitDesign/**ve
plan/v1/billing/V1-BIL-003-discount-fee-lines.md | Done | src/Modules/Billing/Adjustments/**ve
plan/v1/cashier-ui/V1-CUI-001-table-and-session-shell.md | Done | src/Clients/Cashier/TableShell/**, tests/Clients/Cashier/TableShell/**, src/Clients/WebPrototype/**
plan/v1/cashier-ui/V1-CUI-002-order-entry.md | Done | src/Clients/Cashier/OrderEntry/**, tests/Clients/Cashier/OrderEntry/**
plan/v1/cashier-ui/V1-CUI-003-operations-and-print-status.md | Done | src/Clients/Cashier/OperationsStatus/**, tests/Clients/Cashier/OperationsStatus/**
plan/v1/foundation/V1-FND-001-module-skeleton.md | Done | tests/Clients/**/ALKAROS.*.Tests.csproj, tests/Integration/**/ALKAROS.*.Tests.csproj
plan/v1/foundation/V1-FND-002-idempotency-infrastructure.md | Done | src/BuildingBlocks/Idempotency/IdempotencyKey.cs, src/BuildingBlocks/Idempotency/IdempotencyKeyConflictException.cs, src/BuildingBlocks/Idempotency/IdempotencyOutcome.cs, src/BuildingBlocks/Idempotency/IdempotencyStatus.cs, src/BuildingBlocks/Idempotency/RequestHash.cs, src/BuildingBlocks/Messaging/InboxEnvelope.cs, src/BuildingBlocks/Messaging/InboxStatus.cs, tests/BuildingBlocks/Idempotency/EnvelopeValidationTests.cs, tests/BuildingBlocks/Idempotency/IdempotencyKeyTests.cs, tests/BuildingBlocks/Idempotency/RequestHashTests.cs
plan/v1/foundation/V1-FND-010-shared-integration-test-fixtures.md | Done | tests/BuildingBlocks/TestHelpers/Fixtures/RecordingResource.cs, tests/BuildingBlocks/TestHelpers/Fixtures/SimulatedFailures.cs
plan/v1/foundation/V1-FND-011-transaction-outbox-atomicity.md | Done | src/BuildingBlocks/Transactions/DefaultRetryClassifier.cs, src/BuildingBlocks/Transactions/IRetryClassifier.cs, src/BuildingBlocks/Transactions/ITransactionContext.cs, src/BuildingBlocks/Transactions/ITransactionResource.cs, src/BuildingBlocks/Transactions/ITransientFailure.cs, src/BuildingBlocks/Transactions/NestedTransactionException.cs, src/BuildingBlocks/Transactions/RetryClassification.cs, src/BuildingBlocks/Transactions/TransactionExecutionException.cs, src/BuildingBlocks/Transactions/TransactionOptions.cs, src/BuildingBlocks/Transactions/TransactionRetryPolicy.cs, tests/BuildingBlocks/TransactionOutboxIntegration/Fixtures/Sinks.cs, tests/BuildingBlocks/TransactionOutboxIntegration/Fixtures/TransactionOutboxTestDatabase.cs, tests/BuildingBlocks/TransactionOutboxIntegration/TransactionOutboxIntegrationTests.cs, tests/BuildingBlocks/TransactionOutboxIntegration/ALKAROS.TransactionOutboxIntegration.Tests.csproj, tests/BuildingBlocks/Transactions/Concurrency/TransactionConcurrencyTests.cs, tests/BuildingBlocks/Transactions/Propagation/TransactionPropagationTests.cs, tests/BuildingBlocks/Transactions/Retry/TransactionRetryTests.cs, tests/BuildingBlocks/Transactions/ALKAROS.Transactions.Tests.csproj
plan/v1/foundation/V1-FND-014-retry-sql-identifier-hardening.md | Done | tests/BuildingBlocks/Idempotency/RetrySqlIdentifierTests.cs
plan/v1/foundation/V1-FND-015-inbox-idempotency-contract.md | Done | src/BuildingBlocks/Messaging/IInboxHandler.cs
plan/v1/foundation/V1-FND-019-message-lease-fencing.md | Done | src/BuildingBlocks/Messaging/InboxStore.cs, src/BuildingBlocks/Messaging/InboxMessage.cs, tests/BuildingBlocks/Idempotency/InboxStoreTests.cs, tests/BuildingBlocks/Idempotency/OutboxStoreTests.cs, tests/BuildingBlocks/Idempotency/InboxRedeliveryContractTests.cs, tests/BuildingBlocks/Idempotency/RetryPolicyTests.cs, tests/BuildingBlocks/Idempotency/Fixtures/StoreTestDatabase.cs, tests/BuildingBlocks/Idempotency/ALKAROS.Idempotency.Tests.csproj
plan/v1/identity-authorization/V1-IAM-020-manager-decision-surface.md | Done | src/Clients/WaiterPwa/ManagerDecisions/**, tests/Clients/WaiterPwa/ManagerDecisions/**
plan/v1/kitchen-display/V1-RMD-220-persist-and-surface-is-held.md | Done | src/Clients/PosTerminal/.../KitchenOperationsWorkspace.test.tsx
plan/v1/kitchen-printing/V1-KIT-007-ticket-implicit-accept-on-prepare.md | Done | src/Modules/Kitchen/TicketLifecycle/KitchenTicketTests.cs
plan/v1/orders/V1-ORD-002-idempotent-submit.md | Done | src/Modules/Orders/SubmitOrder/**ve
plan/v1/orders/V1-ORD-003-void-and-complimentary.md | Done | src/Modules/Orders/ItemExceptions/**ve
plan/v1/orders/V1-ORD-006-check-detaches-from-table.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-001-consolidated-remediation.md | Done | src/BuildingBlocks/Transactions/TransactionContext.cs, src/BuildingBlocks/Transactions/TransactionScope.cs, src/BuildingBlocks/TransactionOutboxIntegration/TransactionOutbox.cs, src/BuildingBlocks/TransactionOutboxIntegration/TransactionOutboxResource.cs, tests/BuildingBlocks/Transactions/Execution/TransactionExecutionTests.cs
plan/v1/remediation/V1-RMD-002-deep-audit-remediation.md | Done | src/Clients/WaiterPwa/SessionQueue/**, tests/Clients/WaiterPwa/SessionQueue/**, src/Clients/WebPrototype
plan/v1/remediation/V1-RMD-004-mock-runtime-contract-alignment.md | Done | src/Clients/WebPrototype
plan/v1/remediation/V1-RMD-011-web-prototype-hardening-and-touch-targets.md | Done | src/Clients/WebPrototype/index.html, src/Clients/WebPrototype/app.js, src/Clients/WebPrototype/styles.css, src/Clients/WebPrototype/mock-runtime.js, src/Clients/WebPrototype/tests/app.security.test.js, src/Clients/WebPrototype/tests/mock-runtime.test.js, src/Clients/WebPrototype/tests/ui.accessibility.test.js
plan/v1/remediation/V1-RMD-031-complete-containerized-release.md | Done | tests/Clients/PosTerminal/Experience/**
plan/v1/remediation/V1-RMD-035-deep-code-audit-remediation.md | Done | src/BuildingBlocks/Idempotency/IdempotencyKeyStore.cs, tests/BuildingBlocks/Idempotency/IdempotencyKeyStoreTests.cs
plan/v1/remediation/V1-RMD-050-order-endpoint-and-dualscreen-unification.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-053-order-management-store-compilation-alignment.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-054-production-composition-and-order-billing-routing.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-064-order-items-orphan-deletion.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-083-waiter-and-cashier-order-line-note-entry.md | Done | tests/Host/Experience/Orders/OrderManagementExperienceTests.cs
plan/v1/remediation/V1-RMD-097-deep-analysis-wave-24-25-surface-custody.md | Done | tests/BuildingBlocks/Idempotency/OutboxFanoutSinkTests.cs
plan/v1/remediation/V1-RMD-105-low-dead-code-and-misleading-layer-cleanup.md | Done | src/Clients/Cashier/OrderEntry, tests/Clients/Cashier/OrderEntry, src/Clients/WaiterPwa/OrderEntry, tests/Clients/WaiterPwa/OrderEntry
plan/v1/remediation/V1-RMD-106-wave34-independent-audit-critical-and-high-fixes.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-111-waiter-table-serving-ownership-and-transfer.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-113-submit-draft-kitchen-dispatch-and-idempotency.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-118-posterminal-reservation-action-wiring.md | Done | src/Clients/PosTerminal/src/features/tables/tableApi.test.tsx
plan/v1/remediation/V1-RMD-119-catalog-current-price-staleness-and-modifier-group-dead-end.md | Done | src/Clients/PosTerminal/src/features/catalog/models.tsx|.ts, src/Clients/PosTerminal/src/features/catalog/catalogApi.tsx|.ts, src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.tsx|.ts, src/Clients/PosTerminal/src/features/catalog/catalogApi.test.tsx|.ts, src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.test.tsx|.ts
plan/v1/remediation/V1-RMD-128-order-item-availability-check.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-131-menu-management-experience.md | Done | src/Clients/Cashier/MenuRecipeAdmin/**, tests/Clients/Cashier/MenuRecipeAdmin/**
plan/v1/remediation/V1-RMD-132-purchasing-management-experience.md | Done | src/Clients/Cashier/InventoryPurchasing/**, tests/Clients/Cashier/InventoryPurchasing/**
plan/v1/remediation/V1-RMD-133-production-management-experience.md | Done | src/Clients/Cashier/Production/**, tests/Clients/Cashier/Production/**
plan/v1/remediation/V1-RMD-134-buildingblocks-dead-foundation-cleanup.md | Done | tests/BuildingBlocks/Idempotency, src/BuildingBlocks/Transactions/**, src/BuildingBlocks/TransactionOutboxIntegration/**, src/BuildingBlocks/Idempotency/**, tests/BuildingBlocks/Transactions/**, tests/BuildingBlocks/TransactionOutboxIntegration/**, src/BuildingBlocks/Messaging/InboxStore.cs
plan/v1/remediation/V1-RMD-135-waiterpwa-table-amount-and-dead-cashier-engines.md | Done | src/Clients/Cashier/OperationsStatus/**, src/Clients/Cashier/TableShell/**, tests/Clients/Cashier/OperationsStatus/**
plan/v1/remediation/V1-RMD-143-order-accept-real-stock-consumption.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-146-order-item-dto-lossy-projection.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-147-order-line-modifiers-dropped.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-149-pending-qr-order-invisible-to-waiter.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-150-modifier-quantity-and-kitchen-visibility.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-156-database-integrity-constraints.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-159-architecture-boundaries-remainder.md | Done | src/Clients/WaiterPwa/ManagerDecisions, tests/Clients/WaiterPwa/ManagerDecisions, src/Clients/WaiterPwa/OrderStatus, tests/Clients/WaiterPwa/OrderStatus
plan/v1/remediation/V1-RMD-161-modifier-selection-rules.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-162-api-input-bounds-and-error-mapping.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-164-frontend-high-quantity-correction-lost.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-167-independent-review-followups.md | Done | tests/Host/Experience/Orders/OrderManagementExperienceTests.cs
plan/v1/remediation/V1-RMD-168-canvoid-server-derived.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-177-comp-and-transfer-server-clients.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-292-cashier-discount-tip-adjustment-ui.md | Done | tests/E2E/Cashier/specs/15-discount-and-tip.spec.js
plan/v1/remediation/V1-RMD-432-posterminal-lint-accessibility.md | Done | src/Clients/PosTerminal/eslint.config.js
plan/v1/remediation/V1-RMD-440-per-customer-credit-limit-and-overdue-refusal.md | Done | tests/Modules/CustomerAccounts/BillCharges/CreditTermsCreditPolicyTests.cs
plan/v1/remediation/V1-RMD-499-ui-error-text-leaks.md | Done | src/Clients/PosTerminal/src/userMessage.ts, src/Clients/PosTerminal/src/userMessage.test.ts
plan/v1/remediation/V1-RMD-684-settings-edit-screen-and-names.md | Done | src/Clients/PosTerminal/src/features/management-settings-history/SettingEditor.test.tsx
plan/v1/remediation/V1-RMD-686-settings-history-cache-tests-docs.md | Done | tests/Modules/Settings/GarsonFeatureToggles/GarsonFeatureTogglesTests.cs, tests/Modules/Settings/GarsonFeatureToggles/ALKAROS.Settings.GarsonFeatureToggles.Tests.csproj, tests/Modules/Settings/QrOrderExpiry/QrOrderExpirySettingTests.cs, tests/Modules/Settings/QrOrderExpiry/ALKAROS.Settings.QrOrderExpiry.Tests.csproj
plan/v1/remediation/V1-RMD-719-cashier-quick-order-send-integrity.md | Done | src/Modules/Billing/Bills/Bill.cs
plan/v1/remediation/V1-RMD-721-nfc-order-lifecycle-and-errors.md | Done | src/Host/Experience/Orders/PendingOrderConfirmationStore.cs, src/Host/Experience/Orders/QrOrderExpiryHostedService.cs
plan/v1/remediation/V1-RMD-735-posterminal-bolme-ekrani.md | Done | src/Clients/PosTerminal/src/features/billing-split/**
plan/v1/remediation/V1-RMD-738-billing-dokuman-ve-kvkk.md | Done | src/Modules/Billing/BillingModule.cs
plan/v1/table-management/V1-TBL-002-table-transfer.md | Done | src/Modules/Tables/TableTransfer/**ve
plan/v1/table-management/V1-TBL-003-table-merge.md | Done | src/Modules/Tables/TableMerge/**ve
plan/v1/table-management/V1-TBL-004-table-reservation-record.md | Done | src/Modules/Tables/Reservations/**ve
plan/v1/table-management/V1-TBL-005-current-pointer-projection.md | Done | src/Modules/Tables/CurrentPointers/**ve
plan/v1/table-management/V1-TBL-009-recently-vacated-hint.md | Done | tests/Modules/Inventory/*
plan/v1/waiter-pwa/V1-WTR-001-pwa-session-and-offline-queue.md | Done | src/Clients/WaiterPwa/SessionQueue/**ve, tests/Clients/WaiterPwa/SessionQueue/**
plan/v1/waiter-pwa/V1-WTR-002-table-order-entry.md | Done | src/Clients/WaiterPwa/OrderEntry/**, tests/Clients/WaiterPwa/OrderEntry/**
plan/v1/waiter-pwa/V1-WTR-003-order-status.md | Done | src/Clients/WaiterPwa/OrderStatus/**, tests/Clients/WaiterPwa/OrderStatus/**
plan/v1/waiter-pwa/V1-WTR-017-prep-time-delay-warning.md | Done | src/routes/workspace.tsx
plan/v1/waiter-pwa/V1-WTR-031-refactor-step1-order-dto-assembler.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/waiter-pwa/V1-WTR-032-refactor-step2-shift-summary-store.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/waiter-pwa/V1-WTR-033-refactor-step3-cashier-handoff-store.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/waiter-pwa/V1-WTR-034-refactor-step4-table-draft-service.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/waiter-pwa/V1-WTR-035-refactor-step5-order-submission-coordinator.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/waiter-pwa/V1-WTR-036-refactor-step6-7-order-read-store.md | Done | src/Host/Experience/Orders/OrderManagementStore.cs

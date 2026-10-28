# Doğrulanmış bayat md (ölü yol, sıkı kural)
Toplam: 86
plan/v0/governance/V0-GOV-014-messaging-retry-backoff.md | tests/BuildingBlocks/Idempotency/RetryScheduleIntegrationTests.cs
plan/v0/governance/V0-GOV-028-candidate-code-remediation-control.md | tests/Architecture/TaskScope/test_task_scope_remediation_exceptions.py
plan/v1.1/operations-ui/V11-UI-001-menu-and-recipe-ui.md | src/Clients/Cashier/MenuRecipeAdmin/**, tests/Clients/Cashier/MenuRecipeAdmin/**, tests/Clients/Cashier/MenuRecipeAdmin/ALKAROS.Cashier.MenuRecipeAdmin.Tests.csproj, tests/Clients/Cashier/MenuRecipeAdmin/packages.lock.json
plan/v1.1/operations-ui/V11-UI-002-production-ui.md | src/Clients/Cashier/Production/**, tests/Clients/Cashier/Production/**, tests/Clients/Cashier/Production/ALKAROS.Cashier.Production.Tests.csproj, tests/Clients/Cashier/Production/packages.lock.json
plan/v1.1/operations-ui/V11-UI-003-inventory-and-purchasing-ui.md | src/Clients/Cashier/InventoryPurchasing/**, tests/Clients/Cashier/InventoryPurchasing/**, tests/Clients/Cashier/InventoryPurchasing/ALKAROS.Cashier.InventoryPurchasing.Tests.csproj, tests/Clients/Cashier/InventoryPurchasing/packages.lock.json
plan/v1.1/remediation/V11-RMD-002-module-registration-and-inventory-schema-boundary.md | src/Modules/Recipes/Units/UnitDimension.cs
plan/v1.2/online-operations-ui/V12-OUI-001-online-order-operations.md | src/Apps/BackOffice/OnlineOperations/**, tests/Apps/BackOffice/OnlineOperations/**, src/Apps/BackOffice
plan/v1.2/online-ordering/V12-ONL-009-provider-order-polling.md | tests/Modules/OnlineOrdering/Polling/**
plan/v1.2/qr-ordering/V12-QRO-003-confirmation-and-reservation.md | src/Modules/QrOrdering/Confirmation/**, tests/Modules/QrOrdering/Confirmation/**
plan/v1.2/trendyol-go/V12-TGO-002-order-intake.md | tests/Modules/OnlineOrdering/Providers/TrendyolGo/OrderIntake/**
plan/v1.2/trendyol-go/V12-TGO-003-outbound-status.md | tests/Modules/OnlineOrdering/Providers/TrendyolGo/StatusSync/**
plan/v1.2/trendyol-go/V12-TGO-004-menu-availability-and-price.md | tests/Modules/OnlineOrdering/Providers/TrendyolGo/Menu/**
plan/v1.2/trendyol-go/V12-TGO-005-uber-eats-transition-model.md | tests/Modules/OnlineOrdering/Providers/TrendyolGo/
plan/v1.3/payments-ui/V13-PUI-001-split-payment-ui.md | src/Clients/Cashier/Payments/SplitPayment/**
plan/v1.3/payments-ui/V13-PUI-002-cash-session-ui.md | src/Clients/Cashier/Payments/CashSession/
plan/v1.3/payments-ui/V13-PUI-004-eft-payment-ui.md | src/Clients/Cashier/Payments/SplitPayment/**
plan/v1/cashier-ui/V1-CUI-001-table-and-session-shell.md | src/Clients/Cashier/TableShell/**, tests/Clients/Cashier/TableShell/**, src/Clients/WebPrototype/**
plan/v1/cashier-ui/V1-CUI-002-order-entry.md | src/Clients/Cashier/OrderEntry/**, tests/Clients/Cashier/OrderEntry/**
plan/v1/cashier-ui/V1-CUI-003-operations-and-print-status.md | src/Clients/Cashier/OperationsStatus/**, tests/Clients/Cashier/OperationsStatus/**
plan/v1/foundation/V1-FND-001-module-skeleton.md | tests/Integration/**/ALKAROS.*.Tests.csproj
plan/v1/foundation/V1-FND-002-idempotency-infrastructure.md | src/BuildingBlocks/Idempotency/IdempotencyKey.cs, src/BuildingBlocks/Idempotency/IdempotencyKeyConflictException.cs, src/BuildingBlocks/Idempotency/IdempotencyOutcome.cs, src/BuildingBlocks/Idempotency/IdempotencyStatus.cs, src/BuildingBlocks/Idempotency/RequestHash.cs, src/BuildingBlocks/Messaging/InboxEnvelope.cs, src/BuildingBlocks/Messaging/InboxStatus.cs, tests/BuildingBlocks/Idempotency/EnvelopeValidationTests.cs, tests/BuildingBlocks/Idempotency/IdempotencyKeyTests.cs, tests/BuildingBlocks/Idempotency/RequestHashTests.cs
plan/v1/foundation/V1-FND-010-shared-integration-test-fixtures.md | tests/BuildingBlocks/TestHelpers/Fixtures/RecordingResource.cs, tests/BuildingBlocks/TestHelpers/Fixtures/SimulatedFailures.cs
plan/v1/foundation/V1-FND-011-transaction-outbox-atomicity.md | src/BuildingBlocks/Transactions/DefaultRetryClassifier.cs, src/BuildingBlocks/Transactions/IRetryClassifier.cs, src/BuildingBlocks/Transactions/ITransactionContext.cs, src/BuildingBlocks/Transactions/ITransactionResource.cs, src/BuildingBlocks/Transactions/ITransientFailure.cs, src/BuildingBlocks/Transactions/NestedTransactionException.cs, src/BuildingBlocks/Transactions/RetryClassification.cs, src/BuildingBlocks/Transactions/TransactionExecutionException.cs, src/BuildingBlocks/Transactions/TransactionOptions.cs, src/BuildingBlocks/Transactions/TransactionRetryPolicy.cs, tests/BuildingBlocks/TransactionOutboxIntegration/Fixtures/Sinks.cs, tests/BuildingBlocks/TransactionOutboxIntegration/Fixtures/TransactionOutboxTestDatabase.cs, tests/BuildingBlocks/TransactionOutboxIntegration/TransactionOutboxIntegrationTests.cs, tests/BuildingBlocks/TransactionOutboxIntegration/ALKAROS.TransactionOutboxIntegration.Tests.csproj, tests/BuildingBlocks/Transactions/Concurrency/TransactionConcurrencyTests.cs, tests/BuildingBlocks/Transactions/Propagation/TransactionPropagationTests.cs, tests/BuildingBlocks/Transactions/Retry/TransactionRetryTests.cs, tests/BuildingBlocks/Transactions/ALKAROS.Transactions.Tests.csproj
plan/v1/foundation/V1-FND-014-retry-sql-identifier-hardening.md | tests/BuildingBlocks/Idempotency/RetrySqlIdentifierTests.cs
plan/v1/foundation/V1-FND-015-inbox-idempotency-contract.md | src/BuildingBlocks/Messaging/IInboxHandler.cs
plan/v1/foundation/V1-FND-019-message-lease-fencing.md | src/BuildingBlocks/Messaging/InboxStore.cs, src/BuildingBlocks/Messaging/InboxMessage.cs, tests/BuildingBlocks/Idempotency/InboxStoreTests.cs, tests/BuildingBlocks/Idempotency/OutboxStoreTests.cs, tests/BuildingBlocks/Idempotency/InboxRedeliveryContractTests.cs, tests/BuildingBlocks/Idempotency/RetryPolicyTests.cs, tests/BuildingBlocks/Idempotency/Fixtures/StoreTestDatabase.cs, tests/BuildingBlocks/Idempotency/ALKAROS.Idempotency.Tests.csproj
plan/v1/identity-authorization/V1-IAM-020-manager-decision-surface.md | src/Clients/WaiterPwa/ManagerDecisions/**, tests/Clients/WaiterPwa/ManagerDecisions/**
plan/v1/kitchen-printing/V1-KIT-007-ticket-implicit-accept-on-prepare.md | src/Modules/Kitchen/TicketLifecycle/KitchenTicketTests.cs
plan/v1/orders/V1-ORD-006-check-detaches-from-table.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-001-consolidated-remediation.md | src/BuildingBlocks/Transactions/TransactionContext.cs, src/BuildingBlocks/Transactions/TransactionScope.cs, src/BuildingBlocks/TransactionOutboxIntegration/TransactionOutbox.cs, src/BuildingBlocks/TransactionOutboxIntegration/TransactionOutboxResource.cs, tests/BuildingBlocks/Transactions/Execution/TransactionExecutionTests.cs
plan/v1/remediation/V1-RMD-002-deep-audit-remediation.md | src/Clients/WaiterPwa/SessionQueue/**, tests/Clients/WaiterPwa/SessionQueue/**, src/Clients/WebPrototype
plan/v1/remediation/V1-RMD-004-mock-runtime-contract-alignment.md | src/Clients/WebPrototype
plan/v1/remediation/V1-RMD-011-web-prototype-hardening-and-touch-targets.md | src/Clients/WebPrototype/index.html, src/Clients/WebPrototype/app.js, src/Clients/WebPrototype/styles.css, src/Clients/WebPrototype/mock-runtime.js, src/Clients/WebPrototype/tests/app.security.test.js, src/Clients/WebPrototype/tests/mock-runtime.test.js, src/Clients/WebPrototype/tests/ui.accessibility.test.js
plan/v1/remediation/V1-RMD-026-floor-plan-persistence-and-api.md | tests/Modules/Tables/TableLifecycle/FloorPlan*.cs, tests/Host/Experience/Tables/FloorPlan*.cs
plan/v1/remediation/V1-RMD-031-complete-containerized-release.md | tests/Clients/PosTerminal/Experience/**
plan/v1/remediation/V1-RMD-035-deep-code-audit-remediation.md | src/BuildingBlocks/Idempotency/IdempotencyKeyStore.cs, tests/BuildingBlocks/Idempotency/IdempotencyKeyStoreTests.cs
plan/v1/remediation/V1-RMD-050-order-endpoint-and-dualscreen-unification.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-053-order-management-store-compilation-alignment.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-054-production-composition-and-order-billing-routing.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-064-order-items-orphan-deletion.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-083-waiter-and-cashier-order-line-note-entry.md | tests/Host/Experience/Orders/OrderManagementExperienceTests.cs
plan/v1/remediation/V1-RMD-097-deep-analysis-wave-24-25-surface-custody.md | tests/BuildingBlocks/Idempotency/OutboxFanoutSinkTests.cs
plan/v1/remediation/V1-RMD-105-low-dead-code-and-misleading-layer-cleanup.md | src/Clients/Cashier/OrderEntry, tests/Clients/Cashier/OrderEntry, src/Clients/WaiterPwa/OrderEntry, tests/Clients/WaiterPwa/OrderEntry
plan/v1/remediation/V1-RMD-106-wave34-independent-audit-critical-and-high-fixes.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-111-waiter-table-serving-ownership-and-transfer.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-113-submit-draft-kitchen-dispatch-and-idempotency.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-128-order-item-availability-check.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-131-menu-management-experience.md | src/Clients/Cashier/MenuRecipeAdmin/**, tests/Clients/Cashier/MenuRecipeAdmin/**
plan/v1/remediation/V1-RMD-132-purchasing-management-experience.md | src/Clients/Cashier/InventoryPurchasing/**, tests/Clients/Cashier/InventoryPurchasing/**
plan/v1/remediation/V1-RMD-133-production-management-experience.md | src/Clients/Cashier/Production/**, tests/Clients/Cashier/Production/**
plan/v1/remediation/V1-RMD-134-buildingblocks-dead-foundation-cleanup.md | src/BuildingBlocks/Transactions/**, src/BuildingBlocks/TransactionOutboxIntegration/**, src/BuildingBlocks/Idempotency/**, tests/BuildingBlocks/Transactions/**, tests/BuildingBlocks/TransactionOutboxIntegration/**, src/BuildingBlocks/Messaging/InboxStore.cs, tests/BuildingBlocks/Idempotency
plan/v1/remediation/V1-RMD-135-waiterpwa-table-amount-and-dead-cashier-engines.md | src/Clients/Cashier/OperationsStatus/**, src/Clients/Cashier/TableShell/**, tests/Clients/Cashier/OperationsStatus/**
plan/v1/remediation/V1-RMD-143-order-accept-real-stock-consumption.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-146-order-item-dto-lossy-projection.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-147-order-line-modifiers-dropped.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-149-pending-qr-order-invisible-to-waiter.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-150-modifier-quantity-and-kitchen-visibility.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-156-database-integrity-constraints.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-159-architecture-boundaries-remainder.md | src/Clients/WaiterPwa/ManagerDecisions, tests/Clients/WaiterPwa/ManagerDecisions, src/Clients/WaiterPwa/OrderStatus, tests/Clients/WaiterPwa/OrderStatus
plan/v1/remediation/V1-RMD-161-modifier-selection-rules.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-162-api-input-bounds-and-error-mapping.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-164-frontend-high-quantity-correction-lost.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-167-independent-review-followups.md | tests/Host/Experience/Orders/OrderManagementExperienceTests.cs
plan/v1/remediation/V1-RMD-168-canvoid-server-derived.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-177-comp-and-transfer-server-clients.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/remediation/V1-RMD-247-recipe-cost-snapshot-http-surface.md | tests/Host/Experience/Recipes/RecipeCostSnapshot*.cs
plan/v1/remediation/V1-RMD-292-cashier-discount-tip-adjustment-ui.md | tests/E2E/Cashier/specs/15-discount-and-tip.spec.js
plan/v1/remediation/V1-RMD-432-posterminal-lint-accessibility.md | src/Clients/PosTerminal/eslint.config.js
plan/v1/remediation/V1-RMD-440-per-customer-credit-limit-and-overdue-refusal.md | tests/Modules/CustomerAccounts/BillCharges/CreditTermsCreditPolicyTests.cs
plan/v1/remediation/V1-RMD-499-ui-error-text-leaks.md | src/Clients/PosTerminal/src/userMessage.ts, src/Clients/PosTerminal/src/userMessage.test.ts
plan/v1/remediation/V1-RMD-684-settings-edit-screen-and-names.md | src/Clients/PosTerminal/src/features/management-settings-history/SettingEditor.test.tsx
plan/v1/remediation/V1-RMD-686-settings-history-cache-tests-docs.md | tests/Modules/Settings/GarsonFeatureToggles/GarsonFeatureTogglesTests.cs, tests/Modules/Settings/GarsonFeatureToggles/ALKAROS.Settings.GarsonFeatureToggles.Tests.csproj, tests/Modules/Settings/QrOrderExpiry/QrOrderExpirySettingTests.cs, tests/Modules/Settings/QrOrderExpiry/ALKAROS.Settings.QrOrderExpiry.Tests.csproj
plan/v1/remediation/V1-RMD-719-cashier-quick-order-send-integrity.md | src/Modules/Billing/Bills/Bill.cs
plan/v1/remediation/V1-RMD-721-nfc-order-lifecycle-and-errors.md | src/Host/Experience/Orders/PendingOrderConfirmationStore.cs, src/Host/Experience/Orders/QrOrderExpiryHostedService.cs
plan/v1/remediation/V1-RMD-735-posterminal-bolme-ekrani.md | src/Clients/PosTerminal/src/features/billing-split/**
plan/v1/remediation/V1-RMD-738-billing-dokuman-ve-kvkk.md | src/Modules/Billing/BillingModule.cs
plan/v1/waiter-pwa/V1-WTR-001-pwa-session-and-offline-queue.md | tests/Clients/WaiterPwa/SessionQueue/**
plan/v1/waiter-pwa/V1-WTR-002-table-order-entry.md | src/Clients/WaiterPwa/OrderEntry/**, tests/Clients/WaiterPwa/OrderEntry/**
plan/v1/waiter-pwa/V1-WTR-003-order-status.md | src/Clients/WaiterPwa/OrderStatus/**, tests/Clients/WaiterPwa/OrderStatus/**
plan/v1/waiter-pwa/V1-WTR-017-prep-time-delay-warning.md | src/routes/workspace.tsx
plan/v1/waiter-pwa/V1-WTR-031-refactor-step1-order-dto-assembler.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/waiter-pwa/V1-WTR-032-refactor-step2-shift-summary-store.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/waiter-pwa/V1-WTR-033-refactor-step3-cashier-handoff-store.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/waiter-pwa/V1-WTR-034-refactor-step4-table-draft-service.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/waiter-pwa/V1-WTR-035-refactor-step5-order-submission-coordinator.md | src/Host/Experience/Orders/OrderManagementStore.cs
plan/v1/waiter-pwa/V1-WTR-036-refactor-step6-7-order-read-store.md | src/Host/Experience/Orders/OrderManagementStore.cs

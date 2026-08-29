# V1-GOV-026 - Master custody reopen and remediation

- Task ID: V1-GOV-026
- Status: InProgress
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: decision
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

Bağımsız denetimde tespit edilen PosTerminal TypeScript typecheck (`TS2322`) hataları, C# xUnit test `CA1707` analyzer uyarıları, in-memory `OrderManagementStore` eksiklikleri, Cashier/Waiter mock-fallback temizliği ve Order-to-Bill köprüsü nedeniyle `GATE-V1-EXIT` kapısını resmen yeniden açmak; çakışan production yüzeylerini devralarak 6 adımlık sıralı kurtarma zincirini (`V1-RMD-044..048`, `V1-GOV-027`) planlamak ve onaylamak.

## Owned surface

- `plan/v1/governance/V1-GOV-026-master-custody-reopen-and-remediation.md`
- `plan/v1/remediation/V1-RMD-020-production-experience-composition-and-e2e.md`
- `plan/v1/remediation/V1-RMD-031-complete-containerized-release.md`
- `plan/v1/remediation/V1-RMD-034-authoritative-kitchen-station-contract.md`
- `plan/v1/remediation/V1-RMD-035-deep-code-audit-remediation.md`
- `plan/v1/remediation/V1-RMD-037-audit-sanitizer-compilation-and-tests.md`
- `plan/v1/remediation/V1-RMD-038-waiter-pwa-production-route-and-pwa-assets.md`
- `plan/v1/remediation/V1-RMD-040-authoritative-order-to-bill-bridge.md`
- `plan/v1/remediation/V1-RMD-041-bill-split-production-navigation-and-flow.md`
- `plan/v1/remediation/V1-RMD-042-shell-freshness-accessibility-and-wcag.md`
- `plan/v1/cashier-ui/V1-CUI-005-remove-fake-cashier-payment-and-align-v1.md`
- `plan/v1/waiter-pwa/V1-WTR-007-waiter-host-order-and-session-contract.md`
- `plan/v1/waiter-pwa/V1-WTR-008-waiter-pwa-real-api-and-reliable-queue.md`
- `plan/v1/remediation/V1-RMD-044-posterminal-compile-and-floor-plan-freshness.md`
- `plan/v1/remediation/V1-RMD-045-audit-sanitizer-tests-analyzer-clean-build.md`
- `plan/v1/remediation/V1-RMD-046-authoritative-postgresql-order-store.md`
- `plan/v1/remediation/V1-RMD-047-cashier-waiter-fail-closed-mock-cleanup.md`
- `plan/v1/remediation/V1-RMD-048-authoritative-order-to-bill-bridge-flow.md`
- `plan/v1/governance/V1-GOV-027-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-026/**`

## In scope

- `GATE-V1-EXIT` kapısını `Reopened` olarak işaretlemek ve güncel görev matrisini yansıtmak.
- 12 Done görevin production yüzeylerini devralıp yeni kurtarma görevlerine bağlamak.
- `V1-RMD-044..048` ve `V1-GOV-027` görev tanımlarını oluşturmak.
- Katı bağımlılık grafiğini tanımlamak.

## Out of scope

- Production kodunu doğrudan bu governance görevinde değiştirmek.

## Dependencies

- V1-GOV-025

## Deliverables

- Yeniden açılan `GATE-V1-EXIT` ve güncellenen `plan/v1/README.md`.
- 6 yeni kurtarma ve reseal görev dosyası.
- Sıfır hatalı doğrulanmış plan grafiği.

## Acceptance evidence

- `plan_audit_tool.py validate` 0 hata verir.
- `GATE-V1-EXIT` ve `plan/v1/README.md` senkronize olur.

## Handoff

- V1-RMD-044

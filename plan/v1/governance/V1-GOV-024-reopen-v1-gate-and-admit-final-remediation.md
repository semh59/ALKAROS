# V1-GOV-024 - Reopen V1 gate and admit final remediation

- Task ID: V1-GOV-024
- Status: Done
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: decision
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

2026-08-29 derin denetiminde bulunan compile hatası, sahte başarı/ödeme ihlalleri, eksik API/rota entegrasyonları ve custody sapmaları nedeniyle `GATE-V1-EXIT` kapısını resmen yeniden açmak; 12 adımlık sıralı kurtarma ve remediasyon zincirini planlamak.

## Owned surface

- `plan/v1/governance/V1-GOV-024-reopen-v1-gate-and-admit-final-remediation.md`
- `plan/v1/remediation/V1-RMD-035-deep-code-audit-remediation.md`
- `plan/v1/cashier-ui/V1-CUI-004-cashier-quick-pos-frontend.md`
- `plan/v1/waiter-pwa/V1-WTR-006-waiter-pwa-mobile-frontend.md`
- `plan/v1/remediation/V1-RMD-028-desktop-floor-plan-workspace.md`
- `plan/v1/remediation/V1-RMD-027-operational-bill-splitting-api.md`
- `plan/v1/remediation/V1-RMD-029-seat-aware-order-and-bill-split-workspace.md`
- `plan/v1/remediation/V1-RMD-034-authoritative-kitchen-station-contract.md`
- `plan/v1/remediation/V1-RMD-033-first-run-manager-provisioning.md`
- `plan/v1/remediation/V1-RMD-023-production-landmark-and-aria-semantics.md`
- `plan/v1/remediation/V1-RMD-031-complete-containerized-release.md`
- `plan/v1/remediation/V1-RMD-037-audit-sanitizer-compilation-and-tests.md`
- `plan/v1/cashier-ui/V1-CUI-005-remove-fake-cashier-payment-and-align-v1.md`
- `plan/v1/waiter-pwa/V1-WTR-007-waiter-host-order-and-session-contract.md`
- `plan/v1/waiter-pwa/V1-WTR-008-waiter-pwa-real-api-and-reliable-queue.md`
- `plan/v1/remediation/V1-RMD-038-waiter-pwa-production-route-and-pwa-assets.md`
- `plan/v1/remediation/V1-RMD-039-table-floor-plan-integration-and-reservations.md`
- `plan/v1/remediation/V1-RMD-040-authoritative-order-to-bill-bridge.md`
- `plan/v1/remediation/V1-RMD-041-bill-split-production-navigation-and-flow.md`
- `plan/v1/remediation/V1-RMD-042-shell-freshness-accessibility-and-wcag.md`
- `plan/v1/remediation/V1-RMD-043-clean-build-test-migration-provenance.md`
- `plan/v1/governance/V1-GOV-025-final-master-audit-reseal.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-024/**`

## In scope

- `GATE-V1-EXIT` kapısını `Reopened` olarak işaretlemek ve 148+ görevlik güncel matris durumunu yansıtmak.
- P0, P1 ve P2 bulgularını çözecek 11 sıralı alt remediasyon görevini (`V1-RMD-037..043`, `V1-CUI-005`, `V1-WTR-007..008`, `V1-GOV-025`) oluşturmak.
- Katı bağımlılık ağacını tanımlamak.

## Out of scope

- Production C# veya frontend kodunu bu görevde değiştirmek.

## Dependencies

- V1-CUI-004
- V1-WTR-006

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kaydı.
- 11 yeni açık remediasyon görev tanımı.
- Doğrulanmış plan ve dependency grafiği.

## Acceptance evidence

- `plan_audit_tool.py validate` 0 hata verir.
- `GATE-V1-EXIT` ve `plan/v1/README.md` güncel durumla senkronize olur.

## Handoff

- V1-RMD-037

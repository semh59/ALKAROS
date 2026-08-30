# V1-GOV-032 - Master custody reopen and remediation wave 5

- Task ID: V1-GOV-032
- Status: Done
- Assignee: 1dec2ab0-b9bc-4bc0-84d5-c4cabf3e4a6a
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

Derin mimari denetimde tespit edilen üretim kompozisyonu eksiklikleri (Order/Billing rotalarının DualScreenHost'a bağlanmaması), PosTerminal React effect bağımlılık döngüsü, Cashier/Waiter katalog ve ikram uyumsuzlukları, AuditSanitizer tırnaksız regex açığı ve oturum/adisyon atomikliği doğrultusunda `GATE-V1-EXIT` kapısını resmen yeniden açmak ve 5 adımlık nihai kurtarma dalgasını (`V1-RMD-054..057`, `V1-GOV-033`) planlamak.

## Owned surface

- `plan/v1/governance/V1-GOV-032-master-custody-reopen-and-remediation-wave-5.md`
- `plan/v1/remediation/V1-RMD-049-audit-malformed-payload-regex-fix.md`
- `plan/v1/remediation/V1-RMD-050-order-endpoint-and-dualscreen-unification.md`
- `plan/v1/remediation/V1-RMD-051-waiter-pwa-and-cashier-ui-contract-alignment.md`
- `plan/v1/remediation/V1-RMD-052-authoritative-order-to-bill-and-posterminal-flow.md`
- `plan/v1/remediation/V1-RMD-053-order-management-store-compilation-alignment.md`
- `plan/v1/remediation/V1-RMD-054-production-composition-and-order-billing-routing.md`
- `plan/v1/remediation/V1-RMD-055-client-catalog-and-complimentary-alignment.md`
- `plan/v1/remediation/V1-RMD-056-posterminal-infinite-loop-and-billing-participants.md`
- `plan/v1/remediation/V1-RMD-057-audit-sanitizer-unclosed-quotes-and-session-invariants.md`
- `plan/v1/governance/V1-GOV-033-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-032/**`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açmak.
- 180 görevlik güncel matrisi doğrulamak (174 Done, 4 NotApplicable, 5 Planned, 1 InProgress).
- `V1-RMD-054..057` ve `V1-GOV-033` görev dosyalarını oluşturmak.

## Out of scope

- Production kodunu bu governance görevi içinde doğrudan değiştirmek.

## Dependencies

- V1-GOV-031

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve görev matrisi.
- Sıralı kurtarma görevleri (`V1-RMD-054..057`, `V1-GOV-033`).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` 0 hata ve 0 uyarı verir.
- `GATE-V1-EXIT` kapısı açık olarak belgelenir.

## Handoff

- V1-RMD-054

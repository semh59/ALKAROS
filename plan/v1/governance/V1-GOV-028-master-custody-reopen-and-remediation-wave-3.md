# V1-GOV-028 - Master custody reopen and remediation wave 3

- Task ID: V1-GOV-028
- Status: Done
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

Bağımsız denetimde tespit edilen derin teknik eksiklikler (audit malformed regex açığı, OrderManagement/DualScreen ikinci sipariş hattı, Cashier ve Waiter PWA sözleşme uyumsuzlukları, Order-to-Bill domain köprüsü, PosTerminal dinamik bağlamı) doğrultusunda `GATE-V1-EXIT` kapısını yeniden açmak, çakışan yüzeylerin devirlerini resmileştirmek ve 5 adımlık nihai kurtarma dalgasını (`V1-RMD-049..052`, `V1-GOV-029`) planlamak.

## Owned surface

- `plan/v1/governance/V1-GOV-028-master-custody-reopen-and-remediation-wave-3.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-028/**`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açmak.
- 173 görevlik güncel matrisi doğrulamak (163 Done, 4 NotApplicable, 5 Planned, 1 InProgress).
- Tarihsel görevlerden (`V1-RMD-037`, `V1-RMD-045`, `V1-RMD-034`, `V1-RMD-006`, `V1-RMD-046`, `V1-RMD-047`, `V1-WTR-008`, `V1-RMD-048`, `V1-RMD-044`, `V1-RMD-039`) yüzey devirlerini belgelemek.
- `V1-RMD-049..052` ve `V1-GOV-029` görev dosyalarını oluşturmak.

## Out of scope

- Production C# veya TypeScript kodlarını doğrudan bu governance görevi içinde değiştirmek.

## Dependencies

- V1-GOV-027

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve görev matrisi.
- Sıralı kurtarma görevleri (`V1-RMD-049..052`, `V1-GOV-029`).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` 0 hata ve 0 uyarı verir.
- `GATE-V1-EXIT` durumu açık olarak belgelenir.

## Handoff

- V1-RMD-049

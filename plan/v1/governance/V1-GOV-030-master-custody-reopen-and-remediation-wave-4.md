# V1-GOV-030 - Master custody reopen and remediation wave 4

- Task ID: V1-GOV-030
- Status: Done
- Assignee: 1dec2ab0-b9bc-4bc0-84d5-c4cabf3e4a6a
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

Canlı derleme testinde tespit edilen `OrderManagementStore.cs` CS1503 argüman uyuşmazlığı ve değişmez nesne atama eksikliği nedeniyle `GATE-V1-EXIT` kapısını yeniden açmak, `OrderManagementStore.cs` yüzey devrini resmileştirmek ve kurtarma görevlerini (`V1-RMD-053`, `V1-GOV-031`) planlamak.

## Owned surface

- `plan/v1/governance/V1-GOV-030-master-custody-reopen-and-remediation-wave-4.md`
- `plan/v1/remediation/V1-RMD-050-order-endpoint-and-dualscreen-unification.md`
- `plan/v1/remediation/V1-RMD-053-order-management-store-compilation-alignment.md`
- `plan/v1/governance/V1-GOV-031-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-030/**`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açmak.
- 175 görevlik güncel matrisi doğrulamak (172 Done, 4 NotApplicable, 2 Planned, 1 InProgress).
- `V1-RMD-050` görevinden `src/Host/Experience/Orders/OrderManagementStore.cs` yüzey devrini belgelemek.
- `V1-RMD-053` ve `V1-GOV-031` görev dosyalarını oluşturmak.

## Out of scope

- Production C# veya TypeScript kodlarını doğrudan bu governance görevi içinde değiştirmek.

## Dependencies

- V1-GOV-029

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve görev matrisi.
- Sıralı kurtarma görevleri (`V1-RMD-053`, `V1-GOV-031`).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` 0 hata ve 0 uyarı verir.
- `GATE-V1-EXIT` kapısı açık olarak belgelenir.

## Handoff

- V1-RMD-053

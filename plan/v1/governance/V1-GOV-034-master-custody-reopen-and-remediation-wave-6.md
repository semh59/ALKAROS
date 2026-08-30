# V1-GOV-034 - Master custody reopen and remediation wave 6

- Task ID: V1-GOV-034
- Status: Done
- Assignee: 1dec2ab0-b9bc-4bc0-84d5-c4cabf3e4a6a
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

2026-08-31 derin sistem denetiminde tespit edilen bulguların (PosTerminal adisyon senkronizasyonu, katalog fiyat güncellemesi ve `current_price` senkronizasyonu, WaiterPwa çevrimdışı kuyruk veri kaybı koruması, Cashier bekletilen fiş koruması, KitchenTicket `Ready` terfisi) giderilmesi için `GATE-V1-EXIT` kapısını yeniden açmak, 5 yeni kurtarma görevini (`V1-RMD-058..062`) ve kapanış görevini (`V1-GOV-035`) planlamak.

## Owned surface

- `plan/v1/governance/V1-GOV-034-master-custody-reopen-and-remediation-wave-6.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-034/**`

## In scope

- `GATE-V1-EXIT` kapısını yeniden açılmış olarak güncellemek.
- `plan/v1/README.md` dosyasını 186 görevlik yeni V1 kapsamıyla güncellemek.
- 5 kurtarma görevi (`V1-RMD-058..062`) ve 1 kapanış görevi (`V1-GOV-035`) tanımlamak.
- `plan_audit_tool.py validate` çalıştırarak plan bütünlüğünü sıfır hatayla doğrulamak.

## Out of scope

- Production kodu yazmak (ilgili RMD görevlerine aittir).

## Dependencies

- V1-GOV-033

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapısı ve tanımlanmış Dalga 6 görevleri.

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata verir.

## Handoff

- V1-RMD-058

# V1-GOV-044 - Master custody reopen and remediation wave 11

- Task ID: V1-GOV-044
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-09-01

## Goal

Lokanta operasyonunda sık gereken kalem bazlı özel talimat girişini uçtan uca çalışır hale getirmek için `GATE-V1-EXIT` kapısını yeniden açmak: garson ve kasiyer sipariş ekranlarında kalem notu girişi, bu notun sipariş yönetiminde ve mutfak fişinde görünür olması. 2 kurtarma görevi (`V1-RMD-082..083`) ve kapanış görevi (`V1-GOV-045`) planlanır.

## Owned surface

- `plan/v1/governance/V1-GOV-044-master-custody-reopen-and-remediation-wave-11.md`
- `plan/v1/remediation/V1-RMD-082-kitchen-ticket-special-instruction-visibility.md`
- `plan/v1/remediation/V1-RMD-083-waiter-and-cashier-order-line-note-entry.md`
- `plan/v1/governance/V1-GOV-045-wave11-master-audit-reseal-and-gate-closure.md`
- `plan/v1/remediation/V1-RMD-074-kitchen-ticket-target-prep-time-contract.md`
- `plan/v1/remediation/V1-RMD-075-posterminal-kitchen-and-system-health-workspace.md`
- `plan/v1/remediation/V1-RMD-079-deployment-https-and-pwa-offline-field-readiness.md`
- `plan/v1/remediation/V1-RMD-038-waiter-pwa-production-route-and-pwa-assets.md`
- `plan/v1/remediation/V1-RMD-077-cashier-terminal-session-provisioning.md`
- `plan/v1/remediation/V1-RMD-064-order-items-orphan-deletion.md`
- `plan/GATES.md`
- `plan/v1/README.md`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açılmış olarak güncellemek.
- V1 görev matrisini 11. dalga görevleriyle (`V1-RMD-082..083`, `V1-GOV-045`) genişletmek ve sayımı güncellemek.
- 2 kurtarma görevi ile 1 kapanış görevini tanımlamak; her biri tek sahipli ve tek yüzeyli olur.
- Tamamlanmış görevlerden yüzey devirleri: `src/Host/Experience/KitchenOperations/**` ve `tests/Host/Experience/KitchenOperations/**` V1-RMD-074'ten V1-RMD-082'ye; `src/Clients/PosTerminal/src/features/kitchen-operations/**` V1-RMD-075'ten V1-RMD-082'ye; `src/Clients/WaiterPwa/wwwroot/waiter-app.js` V1-RMD-079'dan V1-RMD-083'e; `src/Clients/WaiterPwa/wwwroot/index.html` V1-RMD-038'den V1-RMD-083'e; `src/Clients/Cashier/wwwroot/**` V1-RMD-077'den V1-RMD-083'e; `tests/Host/Experience/Orders/**` V1-RMD-064'ten V1-RMD-083'e.
- `python tools/plan-audit/plan_audit_tool.py validate` komutunu sıfır hata ve sıfır uyarı ile çalıştırmak.

## Out of scope

- Yapısal modifier seçici ve fiyat farkı (priceDelta) entegrasyonu; serbest metin talimatı bu dalganın kapsamıdır.
- PosTerminal DualScreen artışlı sipariş girişine not eklemek; sonraki dalgaya aittir.

## Dependencies

- V1-GOV-043

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.
- Sıralı kurtarma görevleri (`V1-RMD-082..083`) ve kapanış görevi (`V1-GOV-045`).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata ve sıfır uyarı verir.
- `GATE-V1-EXIT` kapısı `plan/GATES.md` ve `plan/v1/README.md` üzerinde açık olarak belgelenir.

## Handoff

- V1-RMD-082

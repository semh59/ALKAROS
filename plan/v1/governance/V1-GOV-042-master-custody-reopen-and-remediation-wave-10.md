# V1-GOV-042 - Master custody reopen and remediation wave 10

- Task ID: V1-GOV-042
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

2026-08-31 incelemesinden kalan altyapı ve süreç maddelerini kapatmak için `GATE-V1-EXIT` kapısını yeniden açmak: sahaya çıkmadan önce HTTPS ve PWA çevrimdışı hazırlığının doğrulanması, kod ve şema kimliklerinin İngilizce-yalnız denetimi ve gelecekte benzer tutarsızlıkları önleyen periyodik denetim aracının kurulması. 3 kurtarma görevi (`V1-RMD-079..081`) ve kapanış görevi (`V1-GOV-043`) planlanır.

## Owned surface

- `plan/v1/governance/V1-GOV-042-master-custody-reopen-and-remediation-wave-10.md`
- `plan/v1/remediation/V1-RMD-079-deployment-https-and-pwa-offline-field-readiness.md`
- `plan/v1/remediation/V1-RMD-080-code-identity-english-only-audit.md`
- `plan/v1/remediation/V1-RMD-081-recurring-consistency-audit-tooling.md`
- `plan/v1/governance/V1-GOV-043-wave10-master-audit-reseal-and-gate-closure.md`
- `plan/v1/remediation/V1-RMD-066-order-management-auth-header.md`
- `plan/v1/remediation/V1-RMD-033-first-run-manager-provisioning.md`
- `plan/v1/identity-authorization/V1-IAM-002-authorization.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-042/**`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açılmış olarak güncellemek.
- V1 görev matrisini 10. dalga görevleriyle (`V1-RMD-079..081`, `V1-GOV-043`) genişletmek ve sayımı güncellemek.
- 3 kurtarma görevi ile 1 kapanış görevini tanımlamak; her biri tek sahipli ve tek yüzeyli olur.
- Tamamlanmış görevlerden yüzey devirleri: `src/Clients/WaiterPwa/wwwroot/waiter-app.js` V1-RMD-066'dan V1-RMD-079'a; `compose.yaml`, `deploy/docker/Caddyfile` ve `deploy/docker/README.md` V1-RMD-033'ten V1-RMD-079'a; `src/Modules/Identity/Authorization/IDenialEventSink.cs` ve `src/Modules/Identity/Authorization/PermissionCodes.cs` V1-IAM-002'den V1-RMD-080'e.
- `python tools/plan-audit/plan_audit_tool.py validate` komutunu sıfır hata ve sıfır uyarı ile çalıştırmak.

## Out of scope

- Production kodunu bu governance görevi içinde doğrudan değiştirmek; kod değişiklikleri ilgili `V1-RMD` görevlerine aittir.
- Performans/yük testi, yedekleme-geri yükleme mekanizması ve incelenmemiş modül domain denetimleri; sonraki dalgalara aittir.

## Dependencies

- V1-GOV-041

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.
- Sıralı kurtarma görevleri (`V1-RMD-079..081`) ve kapanış görevi (`V1-GOV-043`).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata ve sıfır uyarı verir.
- `GATE-V1-EXIT` kapısı `plan/GATES.md` ve `plan/v1/README.md` üzerinde açık olarak belgelenir.

## Handoff

- V1-RMD-079

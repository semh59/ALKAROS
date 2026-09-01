# V1-GOV-038 - Master custody reopen and remediation wave 8

- Task ID: V1-GOV-038
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

2026-08-31 tarihli derin V1 kod incelemesinde tespit edilen kullanıcıya sızan İngilizce arayüz metinleri, çevrilmemiş sistem sağlık durumları, mutfak sağlık panelinin yanlış yerleşimi, mutfak ve masa ekranlarında geçen süre uyarısının olmaması, audit sanitizer fallback regex kırılganlığı ve Cashier istemcisindeki sessiz sahte katalog yedeği bulguları için `GATE-V1-EXIT` kapısını yeniden açmak; 6 kurtarma görevini (`V1-RMD-068..073`) ve kapanış görevini (`V1-GOV-039`) planlamak.

## Owned surface

- `plan/v1/governance/V1-GOV-038-master-custody-reopen-and-remediation-wave-8.md`
- `plan/v1/remediation/V1-RMD-068-ui-style-guide-and-turkish-terminology.md`
- `plan/v1/remediation/V1-RMD-069-kitchen-workspace-turkish-remediation-and-health-relocation.md`
- `plan/v1/remediation/V1-RMD-070-catalog-workspace-turkish-string-remediation.md`
- `plan/v1/remediation/V1-RMD-071-floor-plan-table-time-escalation.md`
- `plan/v1/remediation/V1-RMD-072-audit-sanitizer-malformed-payload-guard.md`
- `plan/v1/remediation/V1-RMD-073-cashier-fail-closed-catalog-and-session-identity.md`
- `plan/v1/governance/V1-GOV-039-wave8-master-audit-reseal-and-gate-closure.md`
- `plan/v1/remediation/V1-RMD-030-menu-management-desktop-quality.md`
- `plan/v1/remediation/V1-RMD-034-authoritative-kitchen-station-contract.md`
- `plan/v1/remediation/V1-RMD-052-authoritative-order-to-bill-and-posterminal-flow.md`
- `plan/v1/remediation/V1-RMD-057-audit-sanitizer-unclosed-quotes-and-session-invariants.md`
- `plan/v1/remediation/V1-RMD-061-cashier-park-ticket-protection.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-038/**`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açılmış olarak güncellemek.
- V1 görev matrisini 8. dalga görevleriyle (`V1-RMD-068..073`, `V1-GOV-039`) genişletmek ve sayımı güncellemek.
- 6 kurtarma görevi ile 1 kapanış görevini tanımlamak; her biri tek sahipli ve tek yüzeyli olur.
- Mevcut sahibi tamamlanmış görevlerden ilgili istemci ve modül yüzeylerini yeni kurtarma görevlerine devretmek: `src/Clients/PosTerminal/src/features/kitchen-operations` V1-RMD-034'ten V1-RMD-069'a, `src/Clients/PosTerminal/src/features/catalog` V1-RMD-030'dan V1-RMD-070'e, `src/Clients/PosTerminal/src/features/tables` V1-RMD-052'den V1-RMD-071'e, `src/Modules/Audit/EventStore/IAuditSanitizer.cs` ve ilgili test dosyası V1-RMD-057'den V1-RMD-072'ye, `src/Clients/Cashier/wwwroot` V1-RMD-061'den V1-RMD-073'e.
- `python tools/plan-audit/plan_audit_tool.py validate` komutunu sıfır hata ve sıfır uyarı ile çalıştırmak.

## Out of scope

- Production kodunu bu governance görevi içinde doğrudan değiştirmek; kod değişiklikleri ilgili `V1-RMD` görevlerine aittir.
- Katalog ürün kullanılabilirliği ("86"), HTTPS sertifika stratejisi, akıllı concurrency birleştirme ve performans/yedekleme/cihaz doğrulaması gibi karar gerektiren maddeler; bunlar sonraki dalgalara aittir.

## Dependencies

- V1-GOV-037

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.
- Sıralı kurtarma görevleri (`V1-RMD-068..073`) ve kapanış görevi (`V1-GOV-039`).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata ve sıfır uyarı verir.
- `GATE-V1-EXIT` kapısı `plan/GATES.md` ve `plan/v1/README.md` üzerinde açık olarak belgelenir.

## Handoff

- V1-RMD-068

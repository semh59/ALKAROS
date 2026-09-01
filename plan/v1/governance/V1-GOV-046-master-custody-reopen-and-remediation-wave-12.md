# V1-GOV-046 - Master custody reopen and remediation wave 12

- Task ID: V1-GOV-046
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-09-01

## Goal

Görev listesinin F bölümündeki incelenmemiş modül domain risklerini kapatmaya başlamak için `GATE-V1-EXIT` kapısını yeniden açmak: Observability, Settings, Reporting, Reconciliation, Cash, Audit modülleri ile Orders domain iş kurallarının sistematik kod incelemesi ve tespit edilen operasyonel etkili bulgunun düzeltilmesi. 2 kurtarma görevi (`V1-RMD-084..085`) ve kapanış görevi (`V1-GOV-047`) planlanır.

## Owned surface

- `plan/v1/governance/V1-GOV-046-master-custody-reopen-and-remediation-wave-12.md`
- `plan/v1/remediation/V1-RMD-084-f-section-module-domain-review.md`
- `plan/v1/remediation/V1-RMD-085-reporting-business-day-close-transactional-integrity.md`
- `plan/v1/governance/V1-GOV-047-wave12-master-audit-reseal-and-gate-closure.md`
- `plan/v1/reporting/V1-RPT-001-operational-report-foundation.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-046/**`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açılmış olarak güncellemek.
- V1 görev matrisini 12. dalga görevleriyle (`V1-RMD-084..085`, `V1-GOV-047`) genişletmek ve sayımı güncellemek.
- Tamamlanmış görevlerden yüzey devirleri: `src/Modules/Reporting/V1Operations/**` ve `tests/Modules/Reporting/V1Operations/**` V1-RPT-001'den V1-RMD-085'e.
- `python tools/plan-audit/plan_audit_tool.py validate` komutunu sıfır hata ve sıfır uyarı ile çalıştırmak.

## Out of scope

- Performans/yük testi, yedekleme-geri yükleme mekanizması, cihaz/tarayıcı matrisi ve KVKK saklama uygulaması; sonraki dalgalara aittir.
- Cash modülünü V1'e bağlamak; V1.2 kapsamındadır.

## Dependencies

- V1-GOV-045

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.
- Sıralı kurtarma görevleri (`V1-RMD-084..085`) ve kapanış görevi (`V1-GOV-047`).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata ve sıfır uyarı verir.
- `GATE-V1-EXIT` kapısı `plan/GATES.md` ve `plan/v1/README.md` üzerinde açık olarak belgelenir.

## Handoff

- V1-RMD-084

# V1-GOV-062 - Master custody reopen and remediation wave 20

- Task ID: V1-GOV-062
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-09-01

## Goal

Semih onayıyla (2026-09-01) `V15-PER-001` tam kritik-yol yük testinin V1 için önemli çekirdeği V1'e çekilir: yazma yolu (masa oturt, kalem ekle, sipariş gönder) production boyutlu veri (yaklaşık 1M sipariş satırı) üzerinde eş zamanlı yük altında ölçülür ve veritabanı kilit/deadlock davranışı gözlemlenir. Ayrıca aynı onayla `V0-CMP-005` erişilebilirlik hedefi kararı (`docs/compliance/accessibility-target.md`) kesinleştirilir. Bu iş için `GATE-V1-EXIT` kapısı yeniden açılır; 1 kurtarma görevi (`V1-RMD-093`) ve kapanış görevi (`V1-GOV-063`) planlanır.

## Owned surface

- `plan/v1/governance/V1-GOV-062-master-custody-reopen-and-remediation-wave-20.md`
- `plan/v1/remediation/V1-RMD-093-v1-critical-path-write-load-test.md`
- `plan/v1/governance/V1-GOV-063-wave20-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-062/**`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açılmış olarak güncellemek.
- V1 görev matrisini 20. dalga görevleriyle (`V1-RMD-093`, `V1-GOV-063`) genişletmek ve sayımı güncellemek.
- 2026-09-01 Semih onayının `docs/compliance/accessibility-target.md` içinde V0-CMP-005-D001 ve EXC-001 için kayda geçtiğini not etmek (edit `V1-RMD-093` kapsamında değildir; bu dosya `V0-CMP-005` sahipliğinde bir decision belgesidir ve onay bu dalgada işlenmiştir).
- `python tools/plan-audit/plan_audit_tool.py validate` komutunu sıfır hata ve sıfır uyarı ile çalıştırmak.

## Out of scope

- `V15-PER-001` owned surface'i (`tests/Performance/CriticalPaths/**`, `docs/performance/V15-PER-001.md`); bu görev oraya yazmaz.
- Çoklu düğüm, uzun soak ve gerçek cihaz ağ ölçümü.
- KVKK saklama uygulaması; 21. dalgaya (`V1-RMD-094`) planlanır.

## Dependencies

- V1-GOV-061

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.
- Sıralı kurtarma görevi (`V1-RMD-093`) ve kapanış görevi (`V1-GOV-063`).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata ve sıfır uyarı verir.
- `GATE-V1-EXIT` kapısı `plan/GATES.md` ve `plan/v1/README.md` üzerinde açık olarak belgelenir.

## Handoff

- V1-RMD-093

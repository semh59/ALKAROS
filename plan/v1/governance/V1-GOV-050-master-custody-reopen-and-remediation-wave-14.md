# V1-GOV-050 - Master custody reopen and remediation wave 14

- Task ID: V1-GOV-050
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-09-01

## Goal

Lokantanın canlıya alınmadan önce eş zamanlı yük altındaki davranışının ölçülmemiş olması riskini kapatmaya başlamak için `GATE-V1-EXIT` kapısını yeniden açmak. Tekrarlanabilir bir yük testi harness'i, kritik yol iş yükü modeli, eş zamanlılık taraması ve ölçülen gecikme yüzdeleri ile go-live temel raporu üretilir. 1 kurtarma görevi (`V1-RMD-087`) ve kapanış görevi (`V1-GOV-051`) planlanır.

## Owned surface

- `plan/v1/governance/V1-GOV-050-master-custody-reopen-and-remediation-wave-14.md`
- `plan/v1/remediation/V1-RMD-087-v1-go-live-load-baseline-and-harness.md`
- `plan/v1/governance/V1-GOV-051-wave14-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-050/**`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açılmış olarak güncellemek.
- V1 görev matrisini 14. dalga görevleriyle (`V1-RMD-087`, `V1-GOV-051`) genişletmek ve sayımı güncellemek.
- `python tools/plan-audit/plan_audit_tool.py validate` komutunu sıfır hata ve sıfır uyarı ile çalıştırmak.

## Out of scope

- `V15-PER-001` kritik yol yük testlerinin tam kapsamını (veritabanı kilit analizi, kaynak tavanları, `GATE-V14-EXIT` bağımlılığı) tamamlamak; `V1-RMD-087` yalnızca go-live temel ölçümünü sağlar ve `V15-PER-001` için girdi üretir.
- Cihaz/tarayıcı matrisi ve KVKK saklama uygulaması; sonraki dalgalara aittir.
- Production ayar değişiklikleri; ayrı kaydedilen kusurlar dışında.

## Dependencies

- V1-GOV-049

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.
- Sıralı kurtarma görevi (`V1-RMD-087`) ve kapanış görevi (`V1-GOV-051`).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata ve sıfır uyarı verir.
- `GATE-V1-EXIT` kapısı `plan/GATES.md` ve `plan/v1/README.md` üzerinde açık olarak belgelenir.

## Handoff

- V1-RMD-087

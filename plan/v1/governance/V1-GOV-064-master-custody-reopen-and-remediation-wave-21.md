# V1-GOV-064 - Master custody reopen and remediation wave 21

- Task ID: V1-GOV-064
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-09-01

## Goal

Semih onayıyla (2026-09-01) `V15-KVK-001`/`V15-KVK-002` KVKK saklama uygulamasının V1 çekirdeği V1'e çekilir: `V0-CMP-003` envanterindeki, V1 kapsamında verisi bulunan ve "sil"/"anonimleştir" disposal'ı olan kişisel veri sınıfları için saklama süresi sonunda çalışan, idempotent, dry-run varsayılanlı, legal-hold koruyucu bir anonimleştirme fiili eklenir. `GATE-V1-EXIT` kapısı yeniden açılır; 1 kurtarma görevi (`V1-RMD-094`) ve kapanış görevi (`V1-GOV-065`) planlanır.

## Owned surface

- `plan/v1/governance/V1-GOV-064-master-custody-reopen-and-remediation-wave-21.md`
- `plan/v1/remediation/V1-RMD-094-kvkk-retention-anonymization-job.md`
- `plan/v1/governance/V1-GOV-065-wave21-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-064/**`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açılmış olarak güncellemek.
- V1 görev matrisini 21. dalga görevleriyle (`V1-RMD-094`, `V1-GOV-065`) genişletmek ve sayımı güncellemek.
- Yüzey devri: `src/Host/Program.cs` yüzeyi `V1-RMD-091`'den `V1-RMD-094`'e devredilir.
- `python tools/plan-audit/plan_audit_tool.py validate` komutunu sıfır hata ve sıfır uyarı ile çalıştırmak.

## Out of scope

- Fiscal fiş, Z raporu ve fatura verisi; `V0-CMP-003` envanterinde "yasal olarak sakla" işaretlidir ve bu iş hiç dokunmaz.
- Provider payload retention/silme; `V15-SEC-003` kapsamındadır.
- Çoklu mağaza checkpoint tabanlı workflow'un tamamı; `V15-KVK-002` kapsamındadır. Bu iş tek mağaza için idempotent temel işi sağlar.

## Dependencies

- V1-GOV-063

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.
- Sıralı kurtarma görevi (`V1-RMD-094`) ve kapanış görevi (`V1-GOV-065`).

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata ve sıfır uyarı verir.
- `GATE-V1-EXIT` kapısı `plan/GATES.md` ve `plan/v1/README.md` üzerinde açık olarak belgelenir.

## Handoff

- V1-RMD-094

# V1-GOV-073 - Master custody reopen and remediation wave 26

- Task ID: V1-GOV-073
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-09-04

## Goal

`V1-IAM-025`'in denetiminde bulunan ve `V1-IAM-026` ile karara bağlanan
grant-class bill adjustment yüzeyi (void/comp, mutfak-sipariş senkronizasyonu,
garson bildirimi) altı yeni görev olarak (`V1-SET-002`, `V1-KIT-005`,
`V1-WTR-009`, `V1-ORD-005`, `V1-BIL-005`, `V1-IAM-027`) kaydedildi; bu, henüz
implementasyon başlamadan `GATE-V1-EXIT`'i (Blocked görevler nedeniyle)
yeniden açar. Bu görev bu açılışı `plan/GATES.md` ve `plan/v1/README.md`
üzerinde resmen kaydeder; kapanış `V1-GOV-074`'e bırakılır.

## Owned surface

- `plan/v1/governance/V1-GOV-073-master-custody-reopen-and-remediation-wave-26.md`
- `plan/v1/governance/V1-GOV-074-wave26-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-073/**`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` üzerinde 26. dalga notu ile
  yeniden açılmış olarak güncellemek.
- `plan/v1/README.md` görev matrisini altı yeni görevle (hepsi Blocked)
  güncellemek, sayaçları güncellemek.
- `V1-GOV-074`'ü Status: Planned olarak, altı görev tamamlanmadan
  kapanamayacak şekilde kaydetmek.

## Out of scope

- Kod değişikliği — altı çocuk görevin kapsamındadır.

## Dependencies

- V1-IAM-026

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.

## Acceptance evidence

- `plan/GATES.md` `GATE-V1-EXIT` satırı 26. dalga yeniden açılış notunu
  içerir.
- `plan/v1/README.md` görev sayacı ve anlatı satırı güncellenmiştir.
- `python tools/plan-audit/plan_audit_tool.py validate` / `verify-manifest`
  sıfır hata; `plan/AUDIT_MANIFEST.json` ve `plan/AUDIT_REPORT.md` yeniden
  üretilmiştir.

## Handoff

- V1-SET-002

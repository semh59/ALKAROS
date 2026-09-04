# V1-GOV-077 - Master custody reopen and remediation wave 28

- Task ID: V1-GOV-077
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-09-04

## Goal

`V1-RMD-100` (`docs/domain/table-reservation-policy.md` doc uzlaştırması,
kod/test/migration diff'i yok) Semih'in bu sohbette verdiği onayla
kapatıldı — V1'in son açık `Planned` görevi. Görev kendi Acceptance
evidence'ında belirttiği üzere, manifest yeniden üretimi `GATE-V1-EXIT`'i
yeniden açar; bu görev o açılışı resmen kaydeder ve kapanışı
`V1-GOV-078`'e bırakır.

## Owned surface

- `plan/v1/governance/V1-GOV-077-master-custody-reopen-and-remediation-wave-28.md`
- `plan/v1/governance/V1-GOV-078-wave28-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `evidence/V1-GOV-077/**`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` üzerinde 28. dalga notu ile
  yeniden açılmış olarak güncellemek.
- `plan/v1/README.md` görev matrisini güncellemek (0 `Planned`, 0 `Blocked`).
- `V1-GOV-078`'i kaydetmek.

## Out of scope

- Kod değişikliği — `V1-RMD-100` zaten yalnız doküman/plan değişikliğiydi.

## Dependencies

- V1-RMD-100

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.

## Acceptance evidence

- `plan/GATES.md` `GATE-V1-EXIT` satırı 28. dalga yeniden açılış notunu
  içerir.
- `plan/v1/README.md` görev sayacı güncellenmiştir.

## Handoff

- V1-GOV-078

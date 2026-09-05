# V1-GOV-087 - Master custody reopen and remediation wave 33

- Task ID: V1-GOV-087
- Status: Done
- Assignee: claude-session-01Dhks7X2RG1fxScJpZRzZiL
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-09-05

## Goal

Semih onayıyla ("Hepsini çöz", 2026-09-05), her iki denetimin geri kalan
Low / kod-kalite maddeleri (ölü OrderEntry motorları, `ItemExceptionHandler`
`IsManagerAuthorized` yanıltıcı katmanı, ölü `IPricingRepository`
Update/Delete) `V1-RMD-105` ile temizlendi. Bu görev o kaydı resmen yapar;
kapanışı `V1-GOV-088`'e bırakır.

## Owned surface

- `plan/v1/governance/V1-GOV-087-master-custody-reopen-and-remediation-wave-33.md`
- `plan/v1/governance/V1-GOV-088-wave33-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` üzerinde 33. dalga notu ile
  yeniden açılmış olarak güncellemek.
- `plan/v1/README.md` görev matrisini `V1-RMD-105` ile güncellemek.
- `V1-GOV-088`'i kaydetmek.

## Out of scope

- Kod değişikliği — `V1-RMD-105`'in kapsamındadır (zaten tamamlanmış).

## Dependencies

- V1-RMD-105

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.

## Acceptance evidence

- `plan/GATES.md` `GATE-V1-EXIT` satırı 33. dalga yeniden açılış notunu
  içerir.
- `plan/v1/README.md` görev sayacı güncellenmiştir.

## Handoff

- V1-GOV-088

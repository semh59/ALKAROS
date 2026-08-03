# V0-GOV-011 - Refresh final audit manifest

- Task ID: V0-GOV-011
- Status: Done
- Assignee: /root
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C37

## Goal

Task-scope remediation sonrasinda audit report ve manifest kayitlarini aktif
Markdown envanteriyle tekrar eslemek.

## Owned surface

- `plan/AUDIT_MANIFEST.json`
- `plan/AUDIT_REPORT.md`
- `evidence/V0-GOV-011/**`
- Bu gorev, baska bir task'in owned surface alanini degistiremez.

## In scope

- Audit report ve manifest uretimi ile hash, UTF-8 ve envanter dogrulamasi.

## Out of scope

- Task davranisi, kaynak PDF, task-scope araci veya gate sonucunu degistirmek.

## Dependencies

- V0-GOV-009
- V0-GOV-010

## Deliverables

- Guncel audit report, manifest ve yeniden uretilebilir komut kaniti.

## Acceptance evidence

- `verify-manifest`, `validate` ve `validate-coverage` exit code `0` verir.
- Audit report aktif Markdown envanterini ve yeni task kayitlarini icerir.

## Handoff

- GATE-V0-EXIT

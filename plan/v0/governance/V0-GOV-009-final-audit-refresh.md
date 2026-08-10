# V0-GOV-009 - Refresh final audit evidence

- Task ID: V0-GOV-009
- Status: Done
- Assignee: /root
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C37

## Goal

Son governance ve coverage degisikliklerinden sonra manifest ve audit raporunu
aktif Markdown envanteriyle yeniden eslemek.

## Owned surface

- `plan/AUDIT_MANIFEST.json`
- `plan/AUDIT_REPORT.md`
- `evidence/V0-GOV-009/**`

## In scope

- Audit report ve manifest uretimi ile hash dogrulamasi.

## Out of scope

- Gate sonucu, task davranisi veya PDF icerigini degistirmek.

## Dependencies

- V0-GOV-007
- V0-GOV-008

## Deliverables

- Guncel audit report, manifest ve komut kaniti.

## Acceptance evidence

- `verify-manifest` ve `validate` exit code `0` verir.

## Handoff

- GATE-V0-EXIT

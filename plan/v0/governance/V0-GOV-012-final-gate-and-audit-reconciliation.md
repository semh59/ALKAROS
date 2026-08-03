# V0-GOV-012 - Reconcile final V0 gate and audit records

- Task ID: V0-GOV-012
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C37

## Goal

Tamamlanmis governance gorevlerinden sonra V0 gate sayimini ve audit
envanterini ayni plan metadata durumuna eslemek.

## Owned surface

- `evidence/v0/gate-v0-exit-closure.md`
- `plan/AUDIT_MANIFEST.json`
- `plan/AUDIT_REPORT.md`
- `evidence/V0-GOV-012/**`
- Bu gorev, baska bir task'in owned surface alanini degistiremez.

## In scope

- V0 task durumlarini mekanik saymak, acik Blocked listesini yenilemek ve
  audit report/manifest hash kayitlarini yeniden uretmek.

## Out of scope

- Gate'i kapatmak, Blocked task durumunu degistirmek, product davranisi veya
  kaynak PDF'yi degistirmek.

## Dependencies

- V0-GOV-010
- V0-GOV-011

## Deliverables

- Guncel acik V0 gate kaydi, audit report, manifest ve komut kaniti.

## Acceptance evidence

- Gate sayimi task metadata ile bire bir eslesir ve gate `Open` kalir.
- `verify-manifest`, `validate` ve `validate-coverage` exit code `0` verir.

## Handoff

- GATE-V0-EXIT

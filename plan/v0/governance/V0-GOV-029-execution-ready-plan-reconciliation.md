# V0-GOV-029 - Reconcile execution-ready plan evidence and ownership

- Task ID: V0-GOV-029
- Status: Done
- Assignee: Semih
- Work type: documentation
- Surface state: Existing

## Source basis

- CORR:C31
- CORR:C32
- CORR:C36

## Goal

Execution-ready planin gercek repository durumu, task ownership'i, test sayilari
ve audit manifest'i ile birebir eslesmesini saglamak; candidate evidence'i
dogrulanmis kapanis kaniti gibi gostermemek.

## Owned surface

- `plan/v0/governance/V0-GOV-015-atomic-migration-history.md`
- `plan/v0/governance/V0-GOV-029-execution-ready-plan-reconciliation.md`
- `plan/EXECUTION_READY_PLAN.md`
- `plan/AUDIT_REPORT.md`
- `plan/AUDIT_MANIFEST.json`
- `evidence/V0-GOV-029/**`
- `V0-GOV-004` tarafindan tamamlanmis plan-ownership duzeltme yuzeyi bu
  goreve devredilmistir; bu gorev product code veya task-scope araci yazamaz.

## In scope

- `HostServiceRegistrationTests.cs` icin tek owner kaydi.
- Güncel build/test sonucunu tarihli eski sayımlardan ayırmak.
- Audit report ve manifest'i mevcut Markdown agacindan yeniden uretmek.
- Plan, coverage ve manifest dogrulamalarini yeniden calistirmak.

## Out of scope

- Product code, test davranisi, migration, task statuslerini `Done` yapmak,
  provider karari veya V0 gate kapanisi.

## Dependencies

- V0-GOV-004
- V0-GOV-028

## Deliverables

- Tek-sahip ownership kaydi, guncel execution-ready plan ve yeniden uretilmis
  audit report/manifest.
- Komut, exit code ve sonuc iceren kanit kaydi.

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir.
- `python tools/plan-audit/plan_audit_tool.py validate-coverage` exit code `0` verir.
- `python tools/plan-audit/plan_audit_tool.py verify-manifest` exit code `0` verir.
- Execution-ready plan, eski evidence sayimini guncel test sonucu diye sunmaz.

## Handoff

- V1-FND-007

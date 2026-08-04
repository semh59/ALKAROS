# V0-GOV-031 verification

Task: `V0-GOV-031` — C42 remediasyon entry-gate onayı (C43).
Date: 2026-08-04
Repo: `https://github.com/semh59/ALKAROS.git` branch `master` @ `9e2a6b6` (plan değişikliği).

## Değişiklikler

- `tools/task-scope/task_scope_tool.py`:
  - `_REMEDIATION_EXCEPTION_ROW` approval tarihi: `2026-08-02|2026-08-03` → `2026-08-02|2026-08-03|2026-08-04`.
  - `_APPROVED_REMEDIATION_TASK_IDS`: `V1-IAM-005`, `V1-FND-013`, `V1-FND-014`, `V1-FND-015` eklendi.
- `plan/GATES.md` `TASK_SCOPE_REMEDIATION_EXCEPTIONS` tablosu: aynı dört kimlik `2026-08-04` kayıtlarıyla eklendi.
- `tests/Architecture/TaskScope/test_task_scope.py` `REMEDIATION_ROWS`: dört `2026-08-04` satırı eklendi.
- `docs/engineering/task-scope-contract.md` ve `plan/VALIDATION_CONTRACT.md`: onay kümesi tarih ifadesi `2026-08-04`'ü kapsar.

## Komutlar ve exit code'lar

```
> py -m pytest tests/Architecture/TaskScope -q
73 passed in 48.90s
exit=0

> py tools/task-scope/task_scope_tool.py --task-id V0-GOV-031 --format text
OK: All changes within scope for V0-GOV-031
exit=0

> py tools/plan-audit/plan_audit_tool.py generate-audit-report
Baseline audit records: 211 | Added Markdown records including report: 188 | Audit findings recorded: 1827
exit=0

> py tools/plan-audit/plan_audit_tool.py generate-manifest
Manifest Markdown files: 399 | Manifest SHA-256: FDCF0BD550DB4DC2B2FE52B28F09CD4BA725DDD5B3A13C93B409D94896B3FF42
exit=0

> py tools/plan-audit/plan_audit_tool.py validate
Validation errors: 0 | Validation warnings: 0
exit=0

> py tools/plan-audit/plan_audit_tool.py validate-coverage
Coverage errors: 0
exit=0

> py tools/plan-audit/plan_audit_tool.py verify-manifest
Manifest errors: 0
exit=0
```

## Entry-gate istisnası kabulü (temiz worktree, commit sonrası)

GOV-031 değişiklikleri commit edilip push edildikten sonra çalıştırıldı:

```
> py tools/task-scope/task_scope_tool.py --task-id V1-IAM-005 --format text
OK: All changes within scope for V1-IAM-005
exit=0

> py tools/task-scope/task_scope_tool.py --task-id V1-FND-013 --format text
OK: All changes within scope for V1-FND-013
exit=0

> py tools/task-scope/task_scope_tool.py --task-id V1-FND-014 --format text
OK: All changes within scope for V1-FND-014
exit=0

> py tools/task-scope/task_scope_tool.py --task-id V1-FND-015 --format text
OK: All changes within scope for V1-FND-015
exit=0
```

Dört C42 kimliği `GATE-V0-EXIT` türetilmiş kontrolü açıkken entry gate'ten
geçer; istisna yalnız kanıtlanmış bulgu remediasyonu içindir (C43).

## Kapsam dışı

- `_CANDIDATE_CODE_REMEDIATION_TASK_IDS` değiştirilmedi.
- V0 task durumları ve gate kapanış kanıtı üretilmedi.

# V0-GOV-035 verification transcript

- Repository: `D:\PROJECT\ALKAROS-REMEDIATION`
- Branch: `codex/audit-remediation`
- Baseline HEAD: `1d41e97b39ac975ab55c2bdf4198b0d6b92681ed`
- Current authority: `CORR:C52`
- Historical PDF is not an admission authority or current evidence source.

## Preflight

```text
git status --short
exit code: 0
stdout: (empty)

git diff --name-only
exit code: 0
stdout: (empty)
```

## Baseline controls

```text
py -m pytest tests/Architecture/TaskScope -q
80 passed in 70.43s
exit code: 0

python -B tools/plan-audit/plan_audit_tool.py validate
Markdown files: 358
Task files: 337
Registered gates: 18
Registered EXT sources: 21
Dependency edges: 1175
Validation errors: 0
Validation warnings: 0
exit code: 0
```

## C52 admission controls

```text
python -m py_compile tools/task-scope/task_scope_tool.py
exit code: 0

python -B tools/task-scope/task_scope_tool.py --task-id V0-GOV-035 --format text
OK: All changes within scope for V0-GOV-035
exit code: 0

py -m pytest tests/Architecture/TaskScope -q
102 passed in 91.43s
exit code: 0

python -B tools/plan-audit/plan_audit_tool.py validate
Markdown files: 358
Task files: 337
Registered gates: 18
Registered EXT sources: 21
Dependency edges: 1175
Validation errors: 0
Validation warnings: 0
exit code: 0

C52 exact-set parity probe
C52_ADMISSION_IDS=18
DONE_ID_INTERSECTION=0
exit code: 0

git diff --check
exit code: 0
```

## Fail-closed coverage

The TaskScope suite proves all 18 current C52 IDs pass the entry-gate fixture,
while duplicate rows, extra IDs, malformed rows, missing markers, a wrong
approval date, a non-C52 source basis, a non-active C52 task, and the historical
`Done` task `V1-FND-001` are rejected.

## Changed-file SHA-256

```text
47C60FBF5DC36A48984B6417D1D597FFCB5D041C702E2BF1091D30C408190EDA  tools/task-scope/task_scope_tool.py
F5586C5FD459793255EC9A6ECF6FDF2507D2CFAB1586781E2351FD69DA25B656  tests/Architecture/TaskScope/test_task_scope.py
B9B7882B6FF174DD92D33A5045CFE86DA951B3CCA94D9F3CFB11EA63E6A1D8F1  docs/engineering/task-scope-contract.md
8AC82754F53A4EDBBE30D7FBBDC82873BAD66491478A605D66E7BF73B5108558  plan/GATES.md
F74482262A84A7B55AB8FE14B35A1B3DAEBA815F0601037E150DF84298374D2B  plan/VALIDATION_CONTRACT.md
025EA06214BDA9562DC6C73021E1DE2EB50740B46B4066299D946387B55ABEE0  plan/v0/governance/V0-GOV-035-remediation-admission-control.md
```

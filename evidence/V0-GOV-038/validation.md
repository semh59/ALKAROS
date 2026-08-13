# V0-GOV-038 Validation

- Candidate measured: `0c8cd75fbebeacfdf455f24de9b13c5ee7434da6`
- Executed: 2026-08-11

```text
python -B tools/task-scope/task_scope_tool.py --task-id V0-GOV-038 --repo-root D:\PROJECT\ALKAROS-REMEDIATION --plan-dir D:\PROJECT\ALKAROS-REMEDIATION\plan --format text
OK: All changes within scope for V0-GOV-038
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

## Closure checks

```text
python -B tools/plan-audit/plan_audit_tool.py validate
Validation errors: 0
Validation warnings: 0
exit code: 0

git diff --check
exit code: 0

candidate_commit_count=157
candidate_history_sha256=4ABB78702C89E7F19BAA9D409CD6B56F0C9D530AF6DA904939435EA728867DBE
preservation_match=True
exit code: 0
```

`task_scope_tool.py` Done metadata'sını aktif yazım durumu saymadığı için
closure sonrası exit code `1` ve `Task status is 'Done', expected 'Planned' or
'InProgress'` üretir. Bu yüzden scope kanıtı, Status `InProgress` iken yukarıda
kayıtlı exit code `0` sonucudur; closure sonrası allowlist kontrolü Git
write-set karşılaştırmasıyla yapılır.

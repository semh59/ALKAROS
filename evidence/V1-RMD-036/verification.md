# V1-RMD-036 Verification Evidence

- Task ID: V1-RMD-036
- Date: 2026-08-29
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3

## 1. Project Manifest Validation

Command:

```bash
python tools/project-manifest/project_manifest_tool.py
```

Output:

```text
ALKAROS Project Manifest Validator
Repository Root: D:\PROJECT\ALKAROS
Solution: ALKAROS.slnx
Status: VALID (0 differences across Solution, Disk, and ProjectReferences)
```

Exit Code: `0`

## 2. Project Manifest Architecture Tests

Command:

```bash
python -m pytest tests/Architecture/ProjectManifest/test_project_manifest.py
```

Output:

```text
4 passed in 1.28s
```

Exit Code: `0`

## 3. Plan Audit Tool Validation

Command:

```bash
python tools/plan-audit/plan_audit_tool.py validate
```

Output:

```text
Markdown files: 443
Task files: 421
Registered gates: 18
Registered EXT sources: 21
Dependency edges: 1409
Validation errors: 0
Validation warnings: 0
```

Exit Code: `0`

## 4. Repo Hygiene

- `output.txt`, `output2.txt`, `test_output.txt` removed from repository tracking.
- `.gitignore` ignores `output*.txt` and `test_output*.txt`.
- Working tree is clean.

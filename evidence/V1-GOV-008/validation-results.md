# V1-GOV-008 validation results

## Outcome

Catalog HTTP remediation custody is singular: the application endpoint and its real HTTP contract test are removed
from V1-RMD-008's forward remediation allowlist and added to V1-RMD-009. V1-RMD-008 remains `Done`; V1-RMD-009 remains
`Planned` and depends on V1-GOV-008.

## Commands and exit codes

1. `python -B tools/plan-audit/plan_audit_tool.py validate`
   - Exit code: `0`
   - Result: 404 Markdown files, 382 task files, 1,329 dependency edges, 0 errors, 0 warnings before final status.
2. `git diff --check -- <V1-RMD-008> <V1-RMD-009>`
   - Exit code: `0`
   - Result: no modified-line whitespace error was reported in the scoped command.
3. `git diff --no-index --check -- NUL <V1-GOV-008>`
   - Normalized exit code: `0` (`git diff --no-index` uses `1` for a clean content difference from `NUL`).
4. `python -B tools/task-scope/task_scope_tool.py --task-id V1-GOV-008 --repo-root . --plan-dir plan --format text`
   - Exit code: `1`
   - Fail-closed reason: the shared worktree contains pre-existing modified/untracked paths outside this task and the
     new task Markdown has no committed baseline. No attempt was made to suppress or absorb those paths.

The full-file `NUL` whitespace check for the two pre-existing untracked remediation documents reports inherited CRLF
lines as trailing whitespace. The scoped modified-line check is clean, and V1-GOV-008's new document is clean.

## Custody assertions

- `src/Host/DualScreen/DualScreenApplication.cs` appears as an Owned surface entry only in V1-RMD-009; V1-RMD-008
  names it only in the narrow custody correction note.
- `tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs` appears as an Owned surface entry only in
  V1-RMD-009; V1-RMD-008 names it only in the narrow custody correction note.
- V1-RMD-008 retains `DualScreenOptions.cs`, `CustomerDisplayContractTests.cs` and `DualScreenOptionsTests.cs`.
- V1-RMD-009 acceptance requires real production HTTP integration coverage for the backward-compatible default
  catalog request, category filter, deterministic cursor continuation and bounded-limit behavior.

## Post-change hashes before final status

- V1-GOV-008: `9182B832CF28F1169FB60116F239495D9F698E0AEC7F2C49748BF62E9A50EAB2`
- V1-RMD-008: `F2CF43760CC476491A35A366650731D1E16025E560B96E16C65A3DA3AB849FC7`
- V1-RMD-009: `ECC84D60CA2ACBE6376E7E4CF660BF4B215104A2B3C0A440FE6237DA65ED3EFD`

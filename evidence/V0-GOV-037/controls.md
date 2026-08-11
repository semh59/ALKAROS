# V0-GOV-037 control transcript

Repository: `D:\PROJECT\ALKAROS-REMEDIATION`

Base commit: `2afa0c3445279be8a5fb3ba80fa2c3d0d22484c6`

## Plan validator

Command: `python -B tools/plan-audit/plan_audit_tool.py validate`

Exit code: `0`

Result: `358` Markdown files, `337` task files, `1175` dependency edges, `0` errors and `0` warnings.

The same command was rerun after `V0-GOV-037` changed to `Done`; it again returned exit code `0` with `0` errors and `0` warnings.

## Ownership transfer control

Read-only control compares 17 exact transferred paths against their historical and C52 task `Owned surface` records, then checks all C52 implementation/integration source/test/migration entries for exact duplicates and wildcard prefix overlap.

Exit code: `0`

Result: `TRANSFER_ROWS=17`, `TRANSFER_ERRORS=0`, `NEW_EXACT_SURFACES=48`, `DUPLICATES=0`, `PREFIX_OVERLAPS=0`.

## Frozen custody and status controls

Read-only control recomputes SHA-256 values from the frozen audit worktree snapshot and compares the 19 historical task headers to `historical-status-preservation.csv`.

Exit code: `0`

Result: `FROZEN_LEDGER_ROWS=32`, `TRACKED=17`, `UNTRACKED=15`, `OUT_OF_SCOPE=16`, `HASH_ERRORS=0`; `STATUS_ROWS=19`, `STATUS_ERRORS=0`.

## Routing and embedded catalog parity

Read-only control compares all CSV owners/prerequisites with JSON `items`, then compares all embedded `task_catalog` dependency arrays with the corresponding Markdown task files.

Exit code: `0`

Result: `ROUTING_CSV_ROWS=42`, `JSON_ITEMS=42`, `ROUTING_ERRORS=0`; `CATALOG_ROWS=68`, `CATALOG_ERRORS=0`.

## Task scope and diff hygiene

Command: `python -B tools/task-scope/task_scope_tool.py --task-id V0-GOV-037 --format text`

Exit code: `0`

Result: `OK: All changes within scope for V0-GOV-037`.

Command: `git diff --check`

Exit code: `0`

# V0-ARC-001 Decision Record — approved

- Task: V0-ARC-001
- Approver: Semih
- Approval date: 2026-08-03
- Source basis: PDF:I.1.1, PDF:I.0, PDF:I.1.4, PDF:I.15, PDF:II.0-II.1, PDF:II.2, PDF:II.5, PDF:III.0-III.2
- Access date: PDF source 2026-07-29; artifact verification 2026-08-02
- Result: Approved
- Artifact: `docs/architecture/module-dependency-rules.md`

## Decision summary

Single communication model per PDF:I.1.1: direct application calls where a
shared transaction boundary requires it + domain/integration events otherwise;
external integrations via Adapter/ACL; no distributed broker; outbox only for
required integration/event flows. Complete per-context interaction list
(26 bounded contexts, rows 1-26) closed in the artifact with PDF:II.2 sources.

## Evidence

- Decision record written to owned surface artifact; status `Done`.
- Full interaction table and rejected alternatives in artifact.
- `python tools/plan-audit/plan_audit_tool.py validate` re-run after closure.

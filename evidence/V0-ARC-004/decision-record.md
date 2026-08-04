# V0-ARC-004 Decision Record — approved

- Task: V0-ARC-004
- Approver: Semih
- Approval date: 2026-08-03
- Source basis: PDF:I.0-I.5, PDF:I.2, PDF:I.15, PDF:II.0-II.1, PDF:II.5, PDF:III.0-III.2, PDF:III.1.6, PDF:III.8.2
- Access date: PDF source 2026-07-29; artifact verification 2026-08-02
- Result: Approved
- Artifact: `docs/architecture/api-contract-standard.md`

## Approved selections

1. URL versioning: path-based (`/api/v1`).
2. Validation: FluentValidation.
3. Headers: `X-Idempotency-Key`, `X-Row-Version`, `X-Correlation-Id`, `X-Request-Id`.
4. Pagination: cursor-based.
5. Error response: custom envelope + error-code catalogue.
6. Event schema: CloudEvents.

Change-breaking rule and two deterministic example contracts
(`POST /api/v1/orders`, `POST /api/v1/bills/{billId}/payments`) in artifact.

## Evidence

- Decision record written to owned surface artifact; status `Done`.
- `python tools/plan-audit/plan_audit_tool.py validate` re-run after closure.

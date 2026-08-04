# V0-DOM-008 Decision Record — approved

- Task: V0-DOM-008
- Approver: Semih
- Approval date: 2026-08-03
- Source basis: PDF:II.2.20, PDF:II.10, PDF:III.31
- Access date: 2026-08-02
- Result: Approved
- Artifact: `docs/domain/reporting-metrics.md`

## Decision summary

- All 16 PDF II.10 reports bound to granularity, filters, timezone/business
  date, source-of-truth and reconciliation total.
- Reporting is derived data; reports never become source of truth.
- Undefined metric stays `Blocked`; no best-effort metrics.
- Business date/timezone per V0-CMP-002; money numeric(18,2).

## Verification

- PDF satırları: II.10 (1383-1387) — report listesi ve "derived data, not
  source of truth" ifadesi; III.31 (backup schema) granularity kaynağı.

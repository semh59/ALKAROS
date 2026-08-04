# V0-CMP-002 Decision Record — approved

- Task: V0-CMP-002
- Approver: Semih
- Approval date: 2026-08-03
- Source basis: PDF:II.0-II.1, PDF:III.0-III.2
- Access date: 2026-08-02
- Result: Approved
- Artifact: `docs/compliance/money-tax-business-date.md`

## Decision summary

- Default KDV `%10` for restaurant/cafe food and non-alcoholic beverage
  service (2007/13033 BKK Liste II sıra 24; 7346 sayılı Karar, 10/07/2023);
  `%20` for alcoholic service; `%1` for basic food. Tax codes are data.
- Tax-inclusive line prices; per-line tax lines on fiscal output.
- Money `numeric(18,2)` TRY; never float/double (PDF III.1.5, III.37).
- UTC storage, business timezone `Europe/Istanbul`, service-day cutoff
  23:59:59 local (PDF III.1.4, III.37).
- Per-line round-half-up to kuruş; bill total is the sum of rounded lines.
- Refund and discount distribution follow the same per-line rounding.
- Rejected alternatives and invariants for V1-CAT-001, V1-BIL-001,
  V12-PAY-001 are recorded in the artifact.

## Verification

- PDF `III.1.5 Money` (line 1491) and `III.37` (line 2424) confirmed
  `numeric(18,2)` and UTC; official KDV rates re-confirmed via 2007/13033
  BKK Liste II sıra 24 and 7346 sayılı Karar.

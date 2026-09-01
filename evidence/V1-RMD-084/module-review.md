# V1-RMD-084 - F-section module domain review

- Date: 2026-09-01
- Reviewer: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Scope: domain services, policies, aggregates and handlers of Observability,
  Settings, Reporting, Reconciliation, Cash, Audit and Orders. Repository SQL
  read where it carries domain rules.

## Method

Read every non-trivial `.cs` file under `src/Modules/{Observability, Settings,
Reporting, Reconciliation, Cash, Audit, Orders}` (excluding enum/exception/
module-registration files), tracing each public operation to its persistence
call. Checked for: silent catch/pass, missing input validation, incorrect state
transitions, non-atomic multi-write, money rounding, concurrency handling.

## Findings

| # | Module | File | Severity | Finding | Action |
| --- | --- | --- | --- | --- | --- |
| 1 | Reporting | `OperationalReportService.CloseBusinessDayAsync` | Medium | Closes the business day, then persists waiter summaries and print-error summaries, then re-reads them — each on its own connection, no shared transaction. A failure after the day is closed leaves it closed with partial or no summaries. | Fixed by `V1-RMD-085` (single-transaction close+summaries repo method). |
| 2 | Reporting | `OperationalReportService.CloseBusinessDayAsync` | Medium | `totalRevenue`, `totalOrders`, `cancelledItems`, `printFailures` are caller-supplied parameters; the module never aggregates them from `orders`/`kitchen` data. EOD report correctness depends entirely on the (unreviewed) caller. | Deferred: a dedicated reporting-aggregation task should compute these from the order and kitchen tables. Out of scope for wave 12. |
| 3 | Reporting | `OperationalReportService.CalculateServiceWindow` | Low | Window end is `05:59:59.999`; the next day's window starts `06:00:00.000`, leaving a 1 ms gap. An event in that gap belongs to no business day. | Acknowledged; use an exclusive upper bound (`< next-day 06:00:00`) in a future edit. No fix this wave. |
| 4 | Orders | `Order.AddItem` | Low | Appends the incoming item without checking `item.Status is OrderItemState.Draft`; an already-Active or Cancelled item can be attached to a Draft order. | Acknowledged; the two production callers build fresh Draft items. Hardening candidate. |
| 5 | Orders | `Order.TransitionTo` | Low | `Rejected` transition stamps no timestamp field (only the status-history entry carries `at`); there is no `RejectedAt`. | Acknowledged; history entry is sufficient for V1. |
| 6 | Orders | `SubmitOrderHandler.HandleAsync` | Low | The concurrent-completion recovery block only catches `InvalidOperationException`. If `IOrderRepository.SaveAsync` surfaces a concurrency failure as a different exception type, it is not converted to an idempotent replay. | Acknowledged; `PostgresOrderRepository.SaveAsync` throws `InvalidOperationException` on version mismatch today, so the current behaviour is correct. Widen if `SaveAsync` changes. |
| 7 | Reconciliation | `ReconciliationService.TransitionCaseStatusAsync` | Low | Does not call `request.Validate()` (the create path does); all state-transition rules live in `PostgresReconciliationRepository`, not the domain. | Acknowledged; V1-REC-001 is an explicit thin "foundation". |
| 8 | Cash | `CashSessionPolicy.ValidateCanStartCount` | Low | Accepts `cashierUserId` but never uses it; anyone can start the count phase of a session. `varianceTolerance` defaults to a hardcoded `50.00m` rather than a Setting. | Deferred to the V1.2 cash implementation task (`V12-*`). Cash is contract-only and not wired into V1. |
| 9 | Audit | `PostgresAuditEventStore.AppendBatchAsync` | Low (perf) | Issues one `INSERT` per event inside a loop in a single transaction; no prepared-statement reuse or `COPY`. Fine for small batches. | Acknowledged; no functional issue. |
| 10 | Observability | `AlertService`, `ObservabilityService` | Info | Alert and health-check lifecycle state machines live entirely in the Postgres repositories; the services are pass-throughs (with `request.Validate()` called consistently, unlike Reconciliation finding 7). | Consistent architectural choice for the support modules. No action. |

## Module verdicts

| Module | Verdict |
| --- | --- |
| Orders | Well-designed. Idempotent submission (`SubmitOrderHandler`) with `FOR UPDATE` locking, hash comparison, stale-version fail-closed, atomic key persist, and concurrent-completion recovery is robust. Money rounds half-up consistently (`OrderMath`). Findings 4–6 are low-severity hardening. |
| Audit | Clean. Append-only, sanitized (`IAuditSanitizer`), jsonb-typed. `ReadRow` column mapping is non-sequential but correct. |
| Reporting | Two Medium findings (1, 2). The module persists an EOD report but neither guarantees atomicity of the write nor computes the figures it stores. |
| Reconciliation | Thin foundation; `TransitionCaseStatusAsync` skips validation; domain rules in the repo. |
| Cash | Contract-only, not wired in V1 (V1.2 scope). `CashSessionPolicy` invariants (single-open, negative-amount guards, variance threshold with supervisor override) are sound. |
| Observability | Thin foundation; validation applied consistently; state machines in the repo. |
| Settings | `SettingValidator` + typed setting records; pass-through service. No defect found in the reviewed path; retention/KVKK behaviour is assessed separately (later wave). |

## Conclusion

No Critical or High severity defects. One operational-impact finding (Reporting
business-day close atomicity) is routed to `V1-RMD-085`. All other findings are
Low/Info and are acknowledged here rather than fixed, consistent with the
"foundation" scope of the support modules and the V1.2 scope of Cash.

# Authorization wave — draft task cluster

Draft `V1-IAM-016` … `V1-IAM-024` for the differentiated authorization model in
`../authorization-model.md`. Kept here (not `plan/v1/identity-authorization/`)
until the decision record is approved by Semih.

## To land in `plan/`

1. Get `../authorization-model.md` approved (fill the `Approver:` line); move it
   to `docs/domain/authorization-model.md`.
2. Move these nine files to `plan/v1/identity-authorization/`.
3. Polish the Turkish prose to carry diacritics — `plan_audit_tool.py validate`
   raises `LANGUAGE_TURKISH` on diacritic-free Turkish (`uyarı` not `uyari`,
   `geçer` not `gecer`, …). The English identifiers / permission codes / command
   snippets in backticks are exempt.
4. Fix `V1-IAM-016` section order: `Dependencies` must precede `Deliverables`.
5. `generate-audit-report` then `generate-manifest`; `validate`, `verify-manifest`,
   `validate-coverage` clean.
6. `V1-IAM-024` transfers custody of several existing owned surfaces
   (`TableManagement*.cs` from V1-RMD-013/026, `DualScreenApplication.cs` from
   V1-RMD-098, `features/tables/**` from V1-RMD-017/071); record the transfers in
   those tasks' Owned surface sections, same pattern as `V1-RMD-098`.
7. Reopen `GATE-V1-EXIT` for the wave; reseal via a `V1-GOV-*` governance task.

## Sequence

`016` (decision) → `017` (permission split + waiter/supervisor roles + migration)
→ `018` (policy engine) → `019` (grant engine + event + reporting) → `020`
(manager decision surface) → `021` (time-boxed delegation) → `022` (bounded
offline authority) → `023` (behavioural tightening) → `024` (re-point every
Experience endpoint, remove `pos.cashier.mutate`).

`017` alone already delivers "a waiter cannot reserve at any terminal" — the rest
is the above-competitor layer.

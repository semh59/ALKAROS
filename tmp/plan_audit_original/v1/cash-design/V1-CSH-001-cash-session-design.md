# V1-CSH-001 - Finalize CashSession design for V1.2

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision

## Source basis

- Master PDF V1 scope plus referenced V0 correction task; undocumented behavior is out of scope.

## Goal

Freeze terminal/cashier ownership, one-open-session rule, cash direction and close permissions without enabling payment.

## Owned surface

- `docs/domain/cash-session-design.md`, `src/Modules/Cash/Contracts/**`, `tests/Modules/Cash/Contracts/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Lifecycle contract, API/event schema and downstream payment dependency.

## Out of scope

- Cash transaction persistence and operational UI.

## Dependencies

- V0-DOM-001,V0-CMP-002,V1-IAM-002

## Deliverables

- V1-CSH-001 için bağlayıcı contract ve contract tests.
- Pozitif/negatif lifecycle örnekleri.
- Tüketici task dependency listesi.

## Acceptance evidence

- Contract tests reject invalid transition/permission and document why implementation waits for V1.2 payment.

## Handoff

- V12-CSH-001 and V12-CSH-002.


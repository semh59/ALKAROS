# V20-DOC-001 - Publish role-based user manual

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: documentation

## Goal

Publish release-matched user instructions for each approved operational role and its recoverable error paths.

## Owned surface

- `docs/user/**`
- Bu görev ürün davranışını veya runbook sahipliğini değiştiremez.

## In scope

- Cashier, waiter, kitchen, inventory, finance and administrator workflows; screenshots; authorization boundaries; user-recoverable errors.

## Out of scope

- Architecture/API reference, infrastructure runbooks and undocumented behavior.

## Dependencies

- V1-CUI-003, V1-WTR-003, V11-UI-003, V12-PUI-003, V13-UI-003, V14-OUI-001

## Deliverables

- Versioned role-based manual and screenshot provenance list.

## Acceptance evidence

- Every documented action is replayed successfully on the release candidate; no instruction requires a permission unavailable to that role.

## Handoff

- V20-UAT-001, V20-UAT-002 and V20-REL-001.

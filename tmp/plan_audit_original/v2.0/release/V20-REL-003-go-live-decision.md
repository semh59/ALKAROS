# V20-REL-003 - Record go-live decision

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: release gate

## Goal

Record an evidence-backed approve or reject decision for the exact immutable release candidate and its rollback window.

## Owned surface

- `release/decisions/go-live/**`
- Bu görev artifact, test sonucu veya kaynak kanıtı değiştiremez.

## In scope

- Gate completeness, approver identities, artifact hash, deployment window, rollback trigger/owner and explicit approve/reject outcome.

## Out of scope

- Silently waiving failed gates, product fixes and production deployment execution.

## Dependencies

- V20-GAT-002, V20-CMP-001, V20-SEC-001

## Deliverables

- Signed go-live decision tied to exact artifact hashes.

## Acceptance evidence

- Approval exists only when every mandatory gate passes and no critical/high defect is open; otherwise the recorded outcome is reject with blocking evidence.

## Handoff

- Authorized deployment procedure outside this planning repository, or blocking task owners.

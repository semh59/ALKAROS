# V20-DOC-002 - Publish technical reference

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: documentation

## Goal

Publish a release-matched architecture, module contract, API/event, data ownership, integration and diagnostic reference.

## Owned surface

- `docs/technical/**`
- Bu görev product contracts, migrations veya runbook içeriğini yeniden tanımlayamaz.

## In scope

- Module/dependency map, API/event schemas, data dictionary/ownership, migration index, integration configuration, observability and diagnostic bundle use.

## Out of scope

- User manual, legal advice, secret values and unimplemented future design.

## Dependencies

- V0-ARC-001, V0-ARC-004, V15-RUN-001, V15-SUP-001

## Deliverables

- Versioned technical reference with generated contract/schema links.

## Acceptance evidence

- Automated link/schema checks pass and a clean reviewer can map every public contract and owned data set to exactly one module and release version.

## Handoff

- V20-REL-001 and V20-GAT-002.

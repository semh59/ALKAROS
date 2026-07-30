# V15-SUP-001 - Implement redacted diagnostic bundle

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Produce a bounded support bundle that diagnoses incidents without exporting secrets, payment payloads or unnecessary personal data.

## Owned surface

- `src/Modules/Support/DiagnosticBundle/**`, `tests/Modules/Support/DiagnosticBundle/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Health summary, version/config fingerprints, selected correlation logs, redaction, size/time bounds and bundle audit.

## Out of scope

- Remote shell access, database dumps, automatic external upload and incident resolution.

## Dependencies

- V15-OBS-001, V15-SEC-003, V0-CMP-003

## Deliverables

- Authorized diagnostic bundle command/interface.
- Secret/PII leakage, size-limit, time-window and concurrent-generation tests.

## Acceptance evidence

- Automated seeded-secret scan finds no protected value in the bundle; bundle provenance and requesting actor remain auditable.

## Handoff

- V20-DOC-002 and V20-GAT-002.

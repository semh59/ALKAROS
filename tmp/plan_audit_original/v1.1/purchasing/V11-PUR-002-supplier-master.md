# V11-PUR-002 - Implement Supplier master data

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1.1 module/schema sections plus named correction dependency.

## Goal

Implement minimal supplier identity, tax/contact data, active state and uniqueness under KVKK/data-minimization rules.

## Owned surface

- `src/Modules/Purchasing/Suppliers/**`, `tests/Modules/Purchasing/Suppliers/**`, `database/migrations/V11/V11-PUR-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Supplier code/name, tax identity, contact fields, active state and access control.

## Out of scope

- Supplier payable ledger and incoming invoice matching.

## Dependencies

- V1-IAM-002,V0-CMP-003

## Deliverables

- V11-PUR-002 için production implementation.
- Public contract ve otomatik başarı/ret/concurrency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Duplicate code/tax policy is enforced; inactive supplier rejects new order; protected fields follow access rules.

## Handoff

- V11-PUR-001 and V13-PUR-001.


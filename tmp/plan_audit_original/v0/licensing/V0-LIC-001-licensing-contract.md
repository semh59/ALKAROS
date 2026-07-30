# V0-LIC-001 - Define offline-safe licensing contract

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision

## Source basis

- PDF baseline plus the correction/gap named in this task; unsupported behavior requires business or external evidence.

## Goal

Define one-time license activation, machine binding, offline grace, transfer, support update and failure behavior.

## Owned surface

- `docs/licensing/licensing-contract.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- License signature/hash, branch limit, installation transfer, clock rollback, offline operation and recovery ownership.

## Out of scope

- Subscription billing, DRM bypass or remote shutdown behavior not approved by business.

## Dependencies

- V0-ARC-002,V0-CMP-003

## Deliverables

- V0-LIC-001 için bağlayıcı karar dokümanı.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- Loss of licensing service cannot stop core restaurant operation unexpectedly; invalid license behavior and recovery are explicit.

## Handoff

- V20-LIC-001 and V20-LIC-002.


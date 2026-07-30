# V0-ARC-004 - Define API and event contract standard

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision

## Source basis

- PDF baseline plus the correction/gap named in this task; unsupported behavior requires business or external evidence.

## Goal

Define versioning, validation, error, idempotency, concurrency and pagination rules for module HTTP/event contracts.

## Owned surface

- `docs/architecture/api-contract-standard.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Problem details, error codes, request IDs, row versions, idempotency headers, compatibility and generated schema checks.

## Out of scope

- Feature-specific endpoint names or provider payload mapping.

## Dependencies

- V0-ARC-001,V0-ARC-003

## Deliverables

- V0-ARC-004 için bağlayıcı karar dokümanı.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- Two sample contracts demonstrate deterministic success/error/replay semantics; breaking-change rule is explicit.

## Handoff

- All implementation tasks exposing API or events.


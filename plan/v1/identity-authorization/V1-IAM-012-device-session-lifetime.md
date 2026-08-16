# V1-IAM-012 - Independently verify bounded device-session lifetime

- Task ID: V1-IAM-012
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: integration
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

session lifetime değerinin domain ve PostgreSQL katmanında pozitif, bounded ve UTC tabanlı aynı invariant'ı taşıdığını
bağımsız doğrulamak.

## Owned surface

- `database/migrations/V1/V1-IAM-012/013-device-session-lifetime.up.sql`
- `database/migrations/V1/V1-IAM-012/013-device-session-lifetime.down.sql`
- `tests/Modules/Identity/DeviceSessions/DeviceSessionLifetimeMigrationTests.cs`
- `evidence/V1-IAM-012/**`

## In scope

- `CODE-019` için V1-IAM-006 lifetime invariant'ını additive forward/down migration pair ile PostgreSQL katmanında
  zorunlu kılmak.

## Out of scope

- DeviceSessionService, global manifest, project, lock veya plan dosyası değiştirmek.

## Dependencies

- V0-GOV-035
- V1-RMD-001
- V1-FND-021

## Deliverables

- Additive device-session lifetime migration pair, PostgreSQL lifecycle testleri ve raw transcript.

## Acceptance evidence

- Forward/down/forward lifecycle expires_at > created_at invariant'ını korur.
- Migration testleri ve plan validator exit code `0` verir.

## Handoff

- V0-GOV-045

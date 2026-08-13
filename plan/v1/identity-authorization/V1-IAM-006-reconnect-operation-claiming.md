# V1-IAM-006 - Independently verify reconnect operation claiming

- Task ID: V1-IAM-006
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: integration
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

aynı operation için concurrent reconnect çağrılarında applied count toplamının tam `1` olduğunu bağımsız PostgreSQL
interleaving'iyle doğrulamak.

## Owned surface

- `src/Modules/Identity/DeviceSessions/DeviceSessionService.cs`
- `tests/Modules/Identity/DeviceSessions/DeviceSessionServiceTests.cs`
- `evidence/V1-IAM-006/**`

## In scope

- `CODE-006;CODE-007;CODE-019` için DeviceSessionService write paths'ini tek integration owner olarak atomikleştirmek.
- Reconnect claim, revocation linearization ve lifetime policy'nin task-owned code/test davranışını uygulamak.

## Out of scope

- Owned surface dışındaki device-session migration, project, lock veya plan dosyası değiştirmek.

## Dependencies

- V0-GOV-035
- V1-IAM-003

## Deliverables

- Tek DeviceSessionService integration diff'i, concurrent interleaving tests ve raw transcript.

## Acceptance evidence

- Reconnect applied operation listesi yalnız transaction içinde kazanılan claims'ten oluşur.
- Revoke sonrası stale authentication/reconnect success dönmez; non-positive veya policy-exceeding lifetime reddedilir.
- Focused tests ve `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir.

## Handoff

- V0-GOV-045

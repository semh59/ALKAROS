# V14-MAP-002 - Implement provider status mapping

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Map each validated Yemeksepeti status to one allowed internal command or explicit no-op/reconciliation result.

## Owned surface

- `src/Modules/OnlineOrdering/Yemeksepeti/StatusMapping/**`, `tests/Modules/OnlineOrdering/Yemeksepeti/StatusMapping/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Provider vocabulary version, integration type differences, cancellation reasons and unknown status handling.

## Out of scope

- Webhook authentication and transport retries.

## Dependencies

- V0-YSP-001,V0-DOM-001

## Deliverables

- V14-MAP-002 için production implementation.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Every documented provider status has one outcome; unknown/new status does not mutate Order and creates an actionable alert/case.

## Handoff

- V14-ONL-003.


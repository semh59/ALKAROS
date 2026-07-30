# V14-ONL-001 - Implement Yemeksepeti webhook inbox

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Authenticate and persist each provider event once before asynchronous processing.

## Owned surface

- `src/Modules/OnlineOrdering/Yemeksepeti/WebhookInbox/**`, `tests/Modules/OnlineOrdering/Yemeksepeti/WebhookInbox/**`, `database/migrations/V14/V14-ONL-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Webhook token/signature policy, external event uniqueness, raw payload protection and retry-safe acknowledgement.

## Out of scope

- External order normalization and product mapping.

## Dependencies

- V0-YSP-001,V1-FND-002,V0-CMP-003

## Deliverables

- V14-ONL-001 için production implementation.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Provider retry receives successful replay after durable insert; duplicate event produces one inbox record; invalid auth stores nothing.

## Handoff

- V14-ONL-002.


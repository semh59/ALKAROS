# V0-ARC-003 - Define idempotency inbox and outbox contract

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision

## Goal

Internal command, client retry ve external callback tekrarlarını tek altyapı sözleşmesiyle yönetmek.

## Owned surface

- `docs/architecture/idempotency-inbox-outbox.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Key scope, request hash, response replay, retention, inbox uniqueness, outbox dispatch ve poison event davranışı.

## Out of scope

- Provider-specific payload mapping.

## Dependencies

- V0-ARC-001,V0-ARC-002

## Deliverables

- V0-ARC-003 için bağlayıcı karar veya contract dokümanı.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- Aynı key/farklı body reddedilir; aynı key/aynı body aynı sonucu verir; dispatch kaybı ve duplicate teslim açıklıdır.

## Handoff

- V1-FND-002 ve external channel görevleri.


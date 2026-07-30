# V0-DOM-006 - Define void complimentary and discount policy

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision

## Source basis

- PDF baseline plus the correction/gap named in this task; unsupported behavior requires business or external evidence.

## Goal

Separate void, complimentary, discount, waste and refund with actor, approval, tax and audit effects.

## Owned surface

- `docs/domain/void-complimentary-discount-policy.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Eligibility by kitchen/payment state, reason catalog, approval thresholds, bill/order effects and fiscal consequence.

## Out of scope

- Campaign engine, loyalty and provider promotions.

## Dependencies

- V0-CMP-002,V0-DOM-003

## Deliverables

- V0-DOM-006 için bağlayıcı karar dokümanı.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- The same item cannot be classified by conflicting operations; every zero/negative price effect has one authority and audit rule.

## Handoff

- V1-ORD-003 and V1-BIL-003.


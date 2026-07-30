# V0-DAT-005 - Resolve single-branch and business key strategy

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision

## Source basis

- PDF baseline plus the correction/gap named in this task; unsupported behavior requires business or external evidence.

## Goal

Resolve the conflict between a single-branch product and optional `business_id where readiness matters`.

## Owned surface

- `docs/data/business-scope-key-strategy.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Installation, business and branch identity, unique-key scope and future migration boundary.

## Out of scope

- Multi-tenant SaaS design or multi-branch feature implementation.

## Dependencies

- V0-ARC-001

## Deliverables

- V0-DAT-005 için bağlayıcı karar dokümanı.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- Every unique key has an explicit scope; no table carries an unused optional tenant key; single-branch invariant is enforceable.

## Handoff

- V1-FND-001 and every schema task.


# V0-ARC-005 - Define settings ownership and secret classification

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision

## Source basis

- PDF baseline plus the correction/gap named in this task; unsupported behavior requires business or external evidence.

## Goal

Classify configurable values by module, scope, validation, history and secret-storage prohibition.

## Owned surface

- `docs/architecture/settings-ownership.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Business settings, device/provider references, UI preferences, restart requirements and change audit.

## Out of scope

- Secret values, feature implementation and admin UI.

## Dependencies

- V0-ARC-001,V0-CMP-003

## Deliverables

- V0-ARC-005 için bağlayıcı karar dokümanı.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- Every known setting has owner/type/scope/default/validation; credentials are explicitly excluded from general settings.

## Handoff

- V1-SET-001 and V15-SEC-001.


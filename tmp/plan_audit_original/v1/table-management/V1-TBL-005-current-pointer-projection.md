# V1-TBL-005 - Implement Table current pointer projection

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1 scope plus referenced V0 correction task; undocumented behavior is out of scope.

## Goal

Maintain and rebuild current Order/Bill pointers from authoritative source relationships.

## Owned surface

- `src/Modules/TableManagement/CurrentPointers/**`, `tests/Modules/TableManagement/CurrentPointers/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Atomic updates, stale-pointer detection, rebuild and multiple-open-source policy.

## Out of scope

- Table transfer/merge commands and Order creation.

## Dependencies

- V1-TBL-001,V1-BIL-001,V0-DAT-004

## Deliverables

- V1-TBL-005 için production implementation.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Projection rebuild equals live values; stale cache never changes authoritative Order/Bill ownership.

## Handoff

- V1-CUI-001 and V15-REC-001.


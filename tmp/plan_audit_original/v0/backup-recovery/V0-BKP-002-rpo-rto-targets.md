# V0-BKP-002 - Approve RPO and RTO targets

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision

## Source basis

- PDF baseline plus the correction/gap named in this task; unsupported behavior requires business or external evidence.

## Goal

Convert business tolerance for data loss and downtime into measured RPO/RTO acceptance targets.

## Owned surface

- `docs/recovery/rpo-rto-targets.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Critical data classes, local/off-site cadence, restore priority, responsible approver and measurement method.

## Out of scope

- Backup implementation or unsupported guarantee.

## Dependencies

- V0-BKP-001

## Deliverables

- V0-BKP-002 için bağlayıcı karar dokümanı.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- Targets are numerically approved against the measured V0 restore proof; unmeasured guarantees are absent.

## Handoff

- V15-BKP-001, V15-BKP-002 and V20-DRL-001.


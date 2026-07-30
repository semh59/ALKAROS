# V0-DOM-008 - Define reporting metric contracts

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision

## Source basis

- PDF baseline plus the correction/gap named in this task; unsupported behavior requires business or external evidence.

## Goal

Define formulas and source tables for every report promised in PDF II.10 before report code is written.

## Owned surface

- `docs/domain/reporting-metrics.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Sales, product/category, waiter/table, sell-through, portion, waste, cash, payment mix, settlements, aging, reconciliation, printer and backup metrics.

## Out of scope

- Dashboard layout, BI tool and export format.

## Dependencies

- V0-DAT-004,V0-CMP-002

## Deliverables

- V0-DOM-008 için bağlayıcı karar dokümanı.
- Pozitif/negatif örnekler ve rejected alternatives.
- Tüketici görevler için test edilebilir invariant/output listesi.

## Acceptance evidence

- Each metric has grain, filters, timezone/business-date, source-of-truth and reconciliation total; undefined term remains blocked.

## Handoff

- V1-RPT-001, V11-RPT-001, V12-RPT-001, V13-RPT-001, V14-RPT-001 and V15-RPT-001.


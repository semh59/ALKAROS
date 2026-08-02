# V12-FSC-003 - Compose approved adisyon strategy

- Task ID: V12-FSC-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.29
- PDF:II.2.16
- PDF:II.3.12
- PDF:II.5.4
- PDF:III.19
- CORR:C25

## Goal

V0-CMP-001 tarafından seçilen tam olarak bir adisyon branch'ini fail-closed registry'de etkinleştirmek.

## Owned surface

- `src/Modules/Fiscal/AdisyonStrategy/Composition/**`, `tests/Modules/Fiscal/AdisyonStrategy/Composition/**`,
  `database/migrations/V12/V12-FSC-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Selected branch registration, duplicate/missing branch rejection, typed NotApplicable sonucu ve lifecycle dispatch.

## Out of scope

- T300/QNB protocol mapping, provider transport, applicability kararı ve invoice business logic.

## Dependencies

- GATE-V12-FSC-STRATEGY
- V1-ORD-001
- V12-FSC-001
- V12-FSC-004
- V12-FSC-005

## Deliverables

- `src/Modules/Fiscal/AdisyonStrategy/Composition/**` altında branch registry production code'u ve automated tests.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Tam olarak selected branch çözümlenir; missing/duplicate/wrong branch startup'ı fail-closed durdurur.
- `V12-FSC-004` veya `V12-FSC-005` seçilmemiş branch olarak tarihli `NotApplicable` olabilir. V0-CMP-001 hiçbir
  software-managed lifecycle gerektirmiyorsa iki branch ve bu composition task'ı aynı dated decision ile
  `NotApplicable` olur; boş success implementation üretilmez.

## Handoff

- V12-FSC-002
- V20-CMP-001

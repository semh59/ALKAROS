# V0-GOV-008 - Regenerate PDF coverage evidence

- Task ID: V0-GOV-008
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Existing

## Source basis

- PDF:I-IV
- CORR:C1-C9

## Goal

Kaynak PDF'den uretilen coverage kaydini, gercek heading ve birim ciktisiyla
yeniden eslemek.

## Owned surface

- `plan/PDF_COVERAGE.md`
- `evidence/V0-GOV-008/**`

## In scope

- PDF heading, tablo ve metin birimlerini mevcut dogrulama araci ile uretmek.
- Uretilen coverage kaydini hash ve coverage validation ile kanitlamak.

## Out of scope

- PDF metnini, task davranisini veya provider kararini degistirmek.

## Dependencies

- V0-GOV-005

## Deliverables

- Kaynak PDF ile tam eslesen coverage matrisi ve komut kaniti.

## Acceptance evidence

- `py tools/plan-audit/plan_audit_tool.py validate-coverage` exit code `0` verir.

## Handoff

- V0-GOV-007

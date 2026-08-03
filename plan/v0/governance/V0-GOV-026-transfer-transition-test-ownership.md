# V0-GOV-026 - Transfer transition test ownership

- Task ID: V0-GOV-026
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C37

## Goal

Transition regression test yüzeyinin kalıcı tek sahibi olan `V0-GOV-001`i
korumak için aynı yüzeyi `V0-GOV-024` owned surface listesinden çıkarmak.

## Owned surface

- `plan/v0/governance/V0-GOV-024-blocker-transition-enforcement.md`
- `plan/AUDIT_REPORT.md`
- `plan/AUDIT_MANIFEST.json`
- `evidence/V0-GOV-026/**`

## In scope

- Yalnız duplicate test ownership satırının kaldırılması, audit kaydı ve manifest
  yenilemesi.

## Out of scope

- Test kodu, scope aracı, başka task Markdown dosyası, application kodu veya
  version gate değişikliği.

## Dependencies

- V0-GOV-024

## Deliverables

- `tests/Architecture/TaskScope/test_task_scope_markdown_boundary.py` için tek
  owner olarak `V0-GOV-001`i bırakan güncel plan artifact'leri.

## Acceptance evidence

- `validate` sonucu ilgili test yüzeyinde `SURFACE_DUPLICATE` üretmez.
- `verify-manifest` exit code `0` verir.

## Handoff

- V0-GOV-025

# V0-GOV-027 - Resolve transition test ownership

- Task ID: V0-GOV-027
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C37

## Goal

Transition regression test yüzeyinde tek owner kuralını, duplicate veya
uygulanmamış plan tasklarını gerekçeli biçimde kaldırarak yeniden sağlamak.

## Owned surface

- `plan/v0/governance/V0-GOV-023-blocker-transition-regression-test.md`
- `plan/v0/governance/V0-GOV-024-blocker-transition-enforcement.md`
- `plan/v0/governance/V0-GOV-025-retire-duplicate-transition-test-task.md`
- `plan/v0/governance/V0-GOV-026-transfer-transition-test-ownership.md`
- `plan/AUDIT_REPORT.md`
- `plan/AUDIT_MANIFEST.json`
- `evidence/V0-GOV-027/**`

## In scope

- Duplicate test ownership satırının ve iş üretmemiş duplicate plan tasklarının
  kaldırılması, audit kaydı ve manifest yenilemesi.

## Out of scope

- Test kodu, scope aracı, başka task Markdown dosyası, application kodu veya
  version gate değişikliği.

## Dependencies

- V0-GOV-024

## Deliverables

- Transition test yüzeyini yalnız `V0-GOV-001`e bırakan güncel plan ve audit
  artifact'leri.

## Acceptance evidence

- `validate` sonucu transition test yüzeyinde `SURFACE_DUPLICATE` üretmez.
- `verify-manifest` exit code `0` verir.

## Handoff

- V0-GOV-018

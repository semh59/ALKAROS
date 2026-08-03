# V0-GOV-025 - Retire duplicate transition test task

- Task ID: V0-GOV-025
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C37

## Goal

`V0-GOV-024` tarafından uygulanan aynı transition test yüzeyini ikinci kez
sahiplenen planlanan `V0-GOV-023` görevini gerekçeli olarak kaldırmak.

## Owned surface

- `plan/v0/governance/V0-GOV-023-blocker-transition-regression-test.md`
- `plan/AUDIT_REPORT.md`
- `plan/AUDIT_MANIFEST.json`
- `evidence/V0-GOV-025/**`

## In scope

- Yalnız duplicate plan task kaldırılması, gerekçeli audit kaydı ve manifest
  yenilemesi.

## Out of scope

- Scope aracı, test kodu, başka task Markdown dosyası, application kodu veya
  version gate değişikliği.

## Dependencies

- V0-GOV-024

## Deliverables

- `V0-GOV-023` kaldırma kaydı ve tek owner olarak `V0-GOV-024`e işaret eden
  güncel audit/manifest artifact'leri.

## Acceptance evidence

- Kaldırılan task'ın owned test surface'i başka aktif veya planlanan task ile
  çakışmaz.
- `validate` ve `verify-manifest` exit code `0` verir.

## Handoff

- V0-GOV-018

# V0-GOV-042 - Define and enforce the code-coverage gate

- Task ID: V0-GOV-042
- Status: NotApplicable
- Assignee: Semih (product owner)
- Work type: integration
- Surface state: Planned

## Source basis

- CORR:C52

## Goal

Desteklenen collector/exporter, kapsam dahil projeler ve line/branch threshold için named policy kararı aldıktan sonra
CI coverage gate'ini fail-closed uygulamak.

## Owned surface

- `.github/workflows/task-scope.yml`
- `.config/coverage.runsettings`
- `evidence/V0-GOV-042/**`

## In scope

- Named threshold, project scope, generated-code exclusion ve supported exporter kararını kaydetmek.
- Collector summary'sini machine-readable okuyup line/branch threshold altını non-zero yapmak.
- Eksik/bozuk coverage artifact'ini fail-closed negatif fixture ile test etmek.

## Out of scope

- Named karar olmadan threshold uydurmak.
- Yeni NuGet/package/csproj bağımlılığı eklemek veya test case sayısını coverage saymak.

## Dependencies

- V0-GOV-035
- V0-GOV-041

## Onay

NotApplicable — tek geliştiricili proje, formal CI coverage gate şu an gereksiz.
Approved by Semih — Founder/Product Owner — 2026-08-15. Karar geçerlidir,
yeni provenance paketi beklenmez.

## Deliverables

- Approved coverage policy'yi yansıtan runsettings, CI gate ve raw summary evidence.

## Acceptance evidence

- Approved line/branch eşiklerinin üstündeki fixture geçer, altındaki ve eksik artifact fixture'ı fail-closed
  reddedilir.
- Coverage summary candidate SHA ve test run ile bağlanır.
- Dış araç kurulumu veya undocumented exporter varsayımı yapılmaz.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; kanıtlar yalnız `evidence/V0-GOV-042/**`
  altındadır.

## Handoff

- V0-GOV-045

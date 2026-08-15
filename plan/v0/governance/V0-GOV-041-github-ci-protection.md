# V0-GOV-041 - Require the repository verification workflow

- Task ID: V0-GOV-041
- Status: NotApplicable
- Assignee: Semih (product owner)
- Work type: integration
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

GitHub Actions üzerinde repository'nin zorunlu build, test, format, Markdown ve governance kontrollerini çalıştırmak ve
branch policy'de required check olarak doğrulamak.

## Owned surface

- `.github/workflows/task-scope.yml`
- `evidence/V0-GOV-041/**`

## In scope

- Workflow'a task scope, locked Release build/test, format, markdownlint, project-manifest ve governance semantic
  kontrollerini bağlamak.
- Successful Actions run URL/SHA ve required-check/ruleset readback evidence'ı almak.
- Fork/PR permissions ve secret gereksinimlerini minimum tutmak.

## Out of scope

- Coverage gate veya build provenance semantiğini bu göreve gizlemek.
- GitHub admin sonucunu tahmin etmek ya da local run'ı branch protection kanıtı saymak.

## Dependencies

- V0-GOV-035
- V0-GOV-040
- V0-GOV-043
- V0-GOV-044
- V0-GOV-046

## Onay

NotApplicable — tek geliştiricili proje, formal CI gate şu an gereksiz.
Approved by Semih — Founder/Product Owner — 2026-08-15. Karar geçerlidir,
yeni provenance paketi beklenmez.

## Deliverables

- Tek fail-closed verification workflow'u ve external branch-policy evidence'ı.

## Acceptance evidence

- Candidate SHA için workflow bütün zorunlu adımları exit `0` ile tamamlar.
- Required check adı branch/ruleset readback'inde exact görünür ve failing fixture merge'i engeller.
- Erişim yoksa tahmin yapılmaz; task `Blocked` ve kanıt `UNPROVEN` kalır.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; kanıtlar yalnız `evidence/V0-GOV-041/**`
  altındadır.

## Handoff

- V0-GOV-042
- V0-GOV-047

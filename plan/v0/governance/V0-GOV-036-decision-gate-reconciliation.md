# V0-GOV-036 - Reconcile decision revalidation with effective gates

- Task ID: V0-GOV-036
- Status: Done
- Assignee: Antigravity-v0-gov-036
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C52
- CORR:C54

## Goal

30 additive decision supplement'ını mevcut karar, gate ve effective dependency grafiğiyle uzlaştırmak; source evidence
olmadan hiçbir kararı seçmemek veya gate'i açmamak.

## Owned surface

- `plan/DECISION_REVALIDATION.md`
- `evidence/v0/gate-v0-exit-closure.md`
- `evidence/V0-GOV-036/**`

## In scope

- Her supplement sonucunu `confirms`, `supersedes` veya `conflicts` olarak mevcut task/gate kaydıyla eşlemek.
- Effective status ve transitive gate sonucunu yeni graph ölçümüyle üretmek.
- `plan/GATES.md`ni `V0-GOV-050` çıktısı olarak read-only tüketmek.
- Named approval eksik veya çelişkili kaydı fail-closed tutmak.

## Out of scope

- Decision supplement, production code veya eski task gövdesi değiştirmek.
- Kaynak/approver kanıtı olmadan karar seçmek ya da nominal `Done` durumunu effective `Done` saymak.

## Dependencies

- V0-GOV-035
- V0-GOV-050
- V0-REV-001
- V0-REV-002
- V0-REV-003
- V0-REV-004
- V0-REV-005
- V0-REV-006
- V0-REV-007
- V0-REV-008
- V0-REV-009
- V0-REV-010
- V0-REV-011
- V0-REV-012
- V0-REV-013
- V0-REV-014
- V0-REV-015
- V0-REV-016
- V0-REV-017
- V0-REV-018
- V0-REV-019
- V0-REV-020
- V0-REV-021
- V0-REV-022
- V0-REV-023
- V0-REV-024
- V0-REV-025
- V0-REV-026
- V0-REV-027
- V0-REV-028
- V0-REV-029
- V0-REV-030

## Deliverables

- Güncel `DECISION_REVALIDATION` ve read-only `GATES` girdisine dayalı gate closure reconciliation kayıtları.

## Acceptance evidence

- 30 supplement'ın tamamı exact Task ID ile tek satıra bağlıdır ve belirsiz verdict yoktur.
- Nominal ve effective status ayrı tutulur; unresolved conflict bulunan gate kapalı kalır.
- Plan validation ve effective dependency graph kontrolü exit code `0` verir.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; kanıtlar yalnız `evidence/V0-GOV-036/**`
  altındadır.

## Handoff

- V0-GOV-045

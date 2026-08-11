# V0-GOV-045 - Refresh and verify the committed HEAD audit manifest

- Task ID: V0-GOV-045
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C52
- CORR:C55
- CORR:C56
- CORR:C57
- CORR:C58
- CORR:C59
- CORR:C60
- CORR:C61
- CORR:C62
- CORR:C63

## Goal

Bütün remediation taskleri kapandıktan sonra `plan/AUDIT_REPORT.md` ile `plan/AUDIT_MANIFEST.json` dosyalarını tek committed candidate HEAD için yeniden üretmek ve byte/hash doğrulamasını kapatmak.

## Owned surface

- `plan/AUDIT_REPORT.md`
- `plan/AUDIT_MANIFEST.json`
- `evidence/V0-GOV-045/**`

## In scope

- 56 finding verdict'ini terminal task/evidence ile exact eşlemek.
- Candidate commit, tracked artifact hash'leri ve validation sonuçlarını manifest'e yazmak.
- Manifest'i commit sonrası ayrı temiz worktree'de doğrulamak.

## Out of scope

- Ürün, test, task, gate veya validator dosyası değiştirmek.
- Açık/blocked finding'i `VERIFIED` göstermek ya da self-referential working-tree hash'ini committed HEAD kanıtı saymak.

## Dependencies

- V0-GOV-036
- V0-GOV-037
- V0-GOV-038
- V0-GOV-039
- V0-GOV-049
- V0-GOV-050
- V0-GOV-051
- V0-GOV-052
- V0-GOV-054
- V0-GOV-055
- V0-GOV-056
- V0-GOV-057
- V0-GOV-058
- V0-GOV-059
- V0-GOV-060
- V0-GOV-040
- V0-GOV-041
- V0-GOV-042
- V0-GOV-043
- V0-GOV-044
- V0-GOV-046
- V0-GOV-047
- V0-DAT-007
- V1-FND-016
- V1-FND-017
- V1-FND-018
- V1-FND-019
- V1-FND-020
- V1-FND-021
- V1-FND-022
- V1-FND-023
- V1-IAM-006
- V1-IAM-007
- V1-IAM-008
- V1-IAM-009
- V1-IAM-010
- V1-IAM-011
- V1-IAM-012
- V1-IAM-013
- V1-IAM-014
- V1-SEC-004
- V1-SEC-005
- V1-SEC-006
- V1-CAT-003
- V1-CAT-004
- V1-TBL-006

## Deliverables

- Candidate-bound audit report/manifest ve clean-worktree verification transcript'i.

## Acceptance evidence

- 56 finding'in her biri evidence-backed terminal verdict ve owner task'a sahiptir.
- Committed candidate üzerinde manifest verifier exit code `0` ve hash mismatch count `0` verir.
- Report/manifest aynı candidate SHA, finding seti ve verdict toplamlarını taşır.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; kanıtlar yalnız `evidence/V0-GOV-045/**` altındadır.

## Handoff

- V0-GOV-048

# V0-GOV-044 - Close the Markdown lint failures

- Task ID: V0-GOV-044
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: documentation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

Dondurulmuş mandatory Markdown lint kontrolündeki exact ihlalleri içerik anlamını ve historical evidence iddialarını değiştirmeden düzeltmek.

## Owned surface

- `evidence/ENV-001/env001-sdk-repair.md`
- `evidence/ENV-003/env003-test-matrix.md`
- `evidence/V0-GOV-030/verification.md`
- `evidence/V0-GOV-031/verification.md`
- `evidence/V1-FND-001/defect-1-closure.md`
- `evidence/V1-FND-001/verification.md`
- `evidence/V1-FND-002/defect-3-closure.md`
- `evidence/V1-FND-002/defect-4-closure.md`
- `evidence/V1-FND-002/defect-6-closure.md`
- `evidence/V1-FND-003/verification.md`
- `evidence/V1-FND-004/verification.md`
- `evidence/V1-FND-005/defect-5-closure.md`
- `evidence/V1-FND-005/verification.md`
- `evidence/V1-FND-009/closure-report-2026-08-05.md`
- `evidence/V1-FND-013/verification.md`
- `evidence/V1-FND-014/verification.md`
- `evidence/V1-FND-015/verification.md`
- `evidence/V1-IAM-001/closure-2026-08-05.md`
- `evidence/V1-IAM-001/defect-7-closure.md`
- `evidence/V1-IAM-002/closure-2026-08-08.md`
- `evidence/V1-IAM-003/audit-slnx-regression-2026-08-09.md`
- `evidence/V1-IAM-003/closure-2026-08-08.md`
- `evidence/V1-IAM-003/manifest-recover-2026-08-09.md`
- `evidence/V1-IAM-004/closure-2026-08-08.md`
- `evidence/V1-IAM-005/verification.md`
- `evidence/V1-SEC-003/closure-2026-08-05.md`
- `evidence/V1-SEC-003/defect-2-closure.md`
- `plan/v0/governance/V0-GOV-031-c42-entry-gate-approval.md`
- `evidence/V0-GOV-044/**`

## In scope

- Mandatory lint çıktısındaki exact file/rule çiftlerini yeniden ölçmek.
- Yalnız whitespace, heading, list, fence ve line-length gibi semantic olmayan Markdown biçimini düzeltmek.
- TRACEABILITY değişikliğini `V0-GOV-037` tamamlandıktan sonra uygulamak.

## Out of scope

- Evidence verdict, command, exit code, tarih veya hash iddiasını yeniden yazmak.
- Lint config'i gevşetmek, dosya hariç tutmak veya unrelated Markdown formatlamak.

## Dependencies

- V0-GOV-035
- V0-GOV-037

## Deliverables

- Exact 29-path Markdown remediation ve before/after rule ledger.

## Acceptance evidence

- Zorunlu repository Markdown lint komutu timeout olmadan exit code `0` ve issue count `0` verir.
- Her değişiklik lint rule ile birebir bağlıdır; narrative/evidence semantiği için diff review yapılır.
- Plan validation ve task-scope kontrolü exit code `0` verir.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; kanıtlar yalnız `evidence/V0-GOV-044/**` altındadır.

## Handoff

- V0-GOV-041
- V0-GOV-045

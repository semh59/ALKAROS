# V1-FND-026 - Admit explicit root integration custody

- Task ID: V1-FND-026
- Status: Done
- Assignee: Codex-/root
- Work type: validation
- Surface state: Existing

## Goal

Reserved Host project/composition yüzeyinin V1 sonrasında yalnız adı plan denetiminde açıkça kayıtlı bir integration
görevine exact-path sahipliğiyle devredilebilmesini sağlamak; feature wildcard'larının root dosyalarına erişimini
kapalı tutmak.

## Owned surface

- `tools/plan-audit/plan_audit_tool.py`
- `tests/Architecture/PlanAudit/test_plan_audit.py`
- `evidence/V1-FND-026/**`

## In scope

- Root surface owner allowlist'ine yalnız V1-RMD-006 kimliğini eklemek.
- V1-RMD-006 exact Host project sahipliğini aldığında valid, başka bir feature task aynı yolu aldığında invalid olan
  fail-closed PlanAudit testleri.
- Mevcut V1-FND-001 reserved surface, duplicate/prefix overlap ve unowned production file kontrollerini korumak.

## Out of scope

- Host, client, migration, solution/project/build dosyası veya product behavior değiştirmek.
- Wildcard ile root surface sahipliği vermek veya başka task kimliğini yetkilendirmek.

## Dependencies

- V1-FND-003
- V1-RMD-002
- V1-RMD-005

## Acceptance evidence

- `py -m pytest tests/Architecture/PlanAudit -q` ve `python -B tools/plan-audit/plan_audit_tool.py validate` exit code
  `0` verir.
- Pozitif test V1-RMD-006 exact `src/Host/ALKAROS.Host.csproj` sahipliğini kabul eder; negatif test farklı task veya
  wildcard sahibi için `SEMANTIC_ROOT_OWNERSHIP` üretir.
- Mevcut duplicate, prefix-overlap, unowned file ve V1-FND-001 reserved pattern testleri değişmeden geçer.
- `git diff --check` ve pre-Done task-scope kontrolü exit code `0` verir.

## Handoff

- None

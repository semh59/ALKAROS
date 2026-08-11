# V0-GOV-050 - Admit post-closure C52 remediation

- Task ID: V0-GOV-050
- Status: Done
- Assignee: /root/implement_v0_gov_050
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C52
- CORR:C53
- CORR:C54
- CORR:C55

## Goal

Yeni post-closure test-discovery düzeltmesini yalnız `V1-FND-023` ile C52
admission modeline eklemek; mevcut `Done` görevlerin tekrar açılmasını yine
fail-closed reddetmek.

## Owned surface

- `tools/task-scope/task_scope_tool.py`
- `tests/Architecture/TaskScope/test_task_scope.py`
- `docs/engineering/task-scope-contract.md`
- `plan/GATES.md`
- `plan/TRACEABILITY.md`
- `plan/AUDIT_REMEDIATION_ROUTING.csv`
- `plan/AUDIT_REMEDIATION_ROUTING.json`
- `evidence/V0-GOV-050/**`

## In scope

- C52/C53 strict admission tablosunu existing 18 ID + yalnız `V1-FND-023`
  olmak üzere exact 19 ID'ye güncellemek.
- `V1-FND-023` için source/date/marker ve routing/catalog parity'sini
  doğrulamak.
- Existing `Done` ID, duplicate, extra ID, malformed source/date ve
  non-active task vakalarını fail-closed test etmek.

## Out of scope

- Başka bir C52 candidate ID eklemek, `Done` task'ı yeniden açmak veya test
  discovery kodunu değiştirmek.
- Historical PDF'yi current authority veya acceptance kanıtı saymak.

## Dependencies

- V0-GOV-035
- V0-GOV-037
- V0-GOV-049
- V0-GOV-052

## Deliverables

- Exact 19-ID admission seti, negatif fixtures, routing/catalog parity
  transcript'i ve C53 trace readback'i.

## Acceptance evidence

- Tool, GATES ve routing catalog `V1-FND-023`ü tam bir kez kabul eder.
- Existing `Done` ID'ler ve set dışındaki her ID candidate mode'da reddedilir.
- Relevant pytest, plan validation, pre-Done task-scope ve diff check exit
  code `0` verir; kanıtlar `evidence/V0-GOV-050/**` altındadır.

## Handoff

- V1-FND-023
- V0-GOV-045
- V0-GOV-048

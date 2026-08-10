# V0-GOV-035 - Admit only the approved C52 remediation tasks

- Task ID: V0-GOV-035
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

C52 ile onaylanan exact remediation Task ID kümesini task-scope entry-gate
kontrolüne fail-closed eklemek; tablo, araç, test fixture ve sözleşme
metinlerinin aynı kimlikleri taşımasını sağlamak.

## Owned surface

- `tools/task-scope/task_scope_tool.py`
- `tests/Architecture/TaskScope/test_task_scope.py`
- `docs/engineering/task-scope-contract.md`
- `plan/GATES.md`
- `plan/VALIDATION_CONTRACT.md`
- `evidence/V0-GOV-035/**`

## In scope

- `2026-08-10` C52 approval tarihini strict ayrıştırıcıya eklemek.
- Routing ledger'daki V1 remediation kimliklerini approved ve yalnız gerçekten
  candidate-code olanları candidate kümelerine atomik eklemek.
- `GATES.md` tablosu ile araç sabitlerinin exact set eşitliğini korumak.
- Eksik marker, tarih, duplicate veya fazla kimlikte fail-closed negatif test
  üretmek.

## Out of scope

- Herhangi bir remediation bulgusunu düzeltmek veya gate kapanış kanıtı üretmek.
- Yeni ürün davranışı başlatmak, task dependency'sini atlamak ya da kayıtlı
  küme dışındaki kimliği kabul etmek.

## Dependencies

- V0-GOV-034

## Deliverables

- Exact C52 remediation admission kümesi, strict tablo ayrıştırması, negatif
  fixture'lar ve güncel sözleşme metni.

## Acceptance evidence

- `py -m pytest tests/Architecture/TaskScope -q` exit code `0` verir.
- Bir kayıtlı C52 remediation kimliği doğru modda kabul edilir; kayıt dışı,
  duplicate veya bozuk tarihli kimlik fail-closed reddedilir.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir.
- Komut, exit code ve sonuç `evidence/V0-GOV-035/**` altında kayıtlıdır.

## Handoff

- None

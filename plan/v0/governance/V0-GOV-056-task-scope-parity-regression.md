# V0-GOV-056 - Restore TaskScope post-closure parity regression coverage

- Task ID: V0-GOV-056
- Status: Done
- Assignee: /root/implement_v0_gov_056
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C59

## Goal

`V1-FND-023` C57 source-basis ve post-closure routing/catalog kayıtlarıyla
TaskScope repository parity testinin aynı current contract'ı fail-closed
doğrulamasını sağlamak.

## Owned surface

- `tests/Architecture/TaskScope/test_task_scope.py`
- `evidence/V0-GOV-056/**`

## In scope

- `V1-FND-023` source basis'inin yalnız sıralı
  `CORR:C52`, `CORR:C53`, `CORR:C54`, `CORR:C57` işaretçilerini beklemek.
- Existing 19-ID admission tuple'ını, `POST-CL-002` routing kaydını ve
  `V0-GOV-050` catalog contract'ını değiştirmeden doğrulamak.
- C59 sonrası routing toplamının `42 + 10 = 52`, `POST-CL-010` owner'ının
  yalnız `V0-GOV-056` ve catalog kaydının task path/dependency/closure
  evidence ile eş olduğunu doğrulamak.
- Missing, extra veya out-of-order source marker durumlarını deterministic
  negatif regression vakalarıyla reddetmek.

## Out of scope

- TaskScope tool, plan-audit tool, GATES, validation contract, admission tuple
  veya routing artifactlarını değiştirmek.
- Yeni application exception'ı eklemek, mevcut `Done` task'ı yeniden açmak ya
  da `V1-FND-023` product/test-discovery davranışını değiştirmek.

## Dependencies

- V0-GOV-050
- V0-GOV-054

## Deliverables

- Current four-marker source basis, 19-ID admission, 42+10 routing total ve
  C59 routing/catalog kaydı için fail-closed TaskScope parity regressionleri.

## Acceptance evidence

- `py -m pytest tests/Architecture/TaskScope -q` exit code `0` verir.
- Exact FND-023 source basis, existing 19-ID admission contract ve 42+10=52
  routing/catalog parity geçer; missing, extra veya out-of-order marker
  vakaları deterministic non-zero assertion ile reddedilir.
- `python -B tools/plan-audit/plan_audit_tool.py validate` ve pre-Done
  task-scope diff check exit code `0` verir; kanıtlar yalnız
  `evidence/V0-GOV-056/**` altındadır.

## Handoff

- V0-GOV-045
- V0-GOV-048

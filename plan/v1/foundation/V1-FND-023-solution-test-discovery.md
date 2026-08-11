# V1-FND-023 - Restore solution test discovery

- Task ID: V1-FND-023
- Status: Blocked
- Assignee: /root/implement_v1_fnd_023
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C52
- CORR:C53
- CORR:C54

## Goal

Merkezi build target ile `ALKAROSTest=true` taşıyan gerçek test projelerinin
VSTest tarafından test projesi olarak keşfedilmesini sağlamak; exit `0` ama
test çalışmayan solution test kapısını ortadan kaldırmak.

## Owned surface

- `Directory.Build.targets`
- `tests/Architecture/TestDiscovery/test_solution_test_discovery.py`
- `evidence/V1-FND-023/**`

## In scope

- `ALKAROSTest=true` evaluated MSBuild durumunda `IsTestProject=true` yapmak.
- C54’ün tanıdığı tek-seferlik exact authority dışında `V1-FND-001` reserved build yüzeyine dokunmamak.
- `ALKAROS.TestHelpers.csproj` gibi `ALKAROSTest` taşımayan helper'ın test
  projesi sayılmadığını korumak.
- `dotnet test ALKAROS.slnx --no-restore --list-tests` çıktısında bilinen test
  isimlerinin listelendiğini ve normal solution test çıktısında gerçek test
  summary bulunduğunu doğrulamak.

## Out of scope

- Solution, csproj, package/lock, test source veya product behavior değiştirmek.
- Restore/build başarısızlığını skip veya salt exit-code `0` ile başarılı
  göstermeye çalışmak.

## Dependencies

- V0-GOV-050
- V1-FND-001

## Blocker

- Command: `py -B tools\\plan-audit\\plan_audit_tool.py validate`
- Result: exit code `1` with `APPLICATION_STARTED_BEFORE_V0_EXIT V1-FND-023`.
- Unlock: `V0-GOV-054` plan denetiminin C52/C53/C54 kabulünü tanımasını düzeltmelidir; ancak bu tamamlandığında görev sürdürülebilir.

## Deliverables

- Merkezi discovery düzeltmesi, deterministic regression test ve ham solution
  list/run transcripts.

## Acceptance evidence

- `ALKAROSTest=true` test projeleri evaluated `IsTestProject=true` olur;
  helper false kalır.
- Solution `--list-tests` bilinen test adlarını listeler; normal run gerçek
  discovered/executed/failed summary üretir ve failed count `0` olur.
- İlgili testler, locked/available restore koşulunda solution test, plan
  validation, pre-Done task-scope ve diff check exit `0` verir.

## Handoff

- V0-GOV-040
- V0-GOV-045
- V0-GOV-048

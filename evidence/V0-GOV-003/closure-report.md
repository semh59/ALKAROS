# V0-GOV-003 yürütme kanıtı

## Başlangıç kontrolü

- Repository root: `D:\PROJECT\ALKAROS`
- Active Task ID: `V0-GOV-003`
- İlk yazımdan önce `git status --short` ve `git diff --name-only` kaydedildi.
  Worktree'de bu görev dışındaki `plan/GATES.md`, `plan/TRACEABILITY.md`,
  `plan/VALIDATION_CONTRACT.md`, task-scope aracı ve testleri dahil değişiklikler
  zaten vardı; bunlar korundu.
- `V0-GOV-001` ve `V0-GOV-002` bağımlılıkları işe başlamadan önce `Done` olarak
  okundu.

## Yazma allowlist'i

- `tools/task-scope/task_scope_tool.py`
- `tests/Architecture/TaskScope/test_task_scope.py`
- `docs/engineering/task-scope-contract.md`
- `plan/GATES.md`
- `plan/VALIDATION_CONTRACT.md`
- `plan/TRACEABILITY.md`
- `evidence/V0-GOV-003/**`
- Yalnız aktif görevin `Status` ve `Assignee` metadata satırları.

## Komutlar

| Komut | Exit code | Sonuç |
| --- | --- | --- |
| `py -m pytest tests/Architecture/TaskScope -q` | 0 | `67 passed in 46.36s` |
| `py -m py_compile tools/task-scope/task_scope_tool.py` | 0 | Python syntax kontrolü geçti. |
| `git diff --check -- <V0-GOV-003 allowlist files>` | 0 | Whitespace hatası yok. |
| `py tools/plan-audit/plan_audit_tool.py validate` | 1 | Bu görevin yazılabilir yüzeyi dışındaki 22 genel plan hatası. |

## Plan doğrulayıcı engeli

Plan doğrulayıcı mevcut cross-task ownership/dependency ile language/source
hatalarını raporladı. İki bulgu bu görevden söz ediyor ancak izinli mutation
ile düzeltilemez: `V0-GOV-003` acceptance prose yürütme sırasında immutable'dır;
`tests/Architecture/TaskScope/test_task_scope.py` ayrıca `V0-GOV-002` tarafından
sahiplenilmiştir. Plan düzeyi çakışmalar sahiplerince çözülene kadar görev
`InProgress` kalır.

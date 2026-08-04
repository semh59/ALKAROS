# V1-FND-003 verification

Tarih: 2026-08-04
Yürütme: opencode-v1-fnd-003

## Source

- V0-ARC-001 Done, V1-FND-001 Done, V1-FND-010 Done — tam dependency zinciri kapalı.
- Blocker bölümü kaldırıldı; Blocked -> InProgress -> Done.

## Owned surface ve mevcut durum

| Owned surface | Durum |
|---|---|
| `tools/task-scope/**` | `task_scope_tool.py` mevcut, tüm fail-closed kontrolleri içerir; V0-GOV-031/032 ile C44 devir istisnası eklendi |
| `tests/Architecture/TaskScope/test_task_scope_diff.py` | Mevcut, 9 test (DiffMode 5 + AllowlistShapeFilter 4) |
| `.github/workflows/task-scope.yml` | Mevcut, PR + workflow_dispatch tetikleyicili, windows-latest + pwsh |
| `docs/engineering/task-scope-contract.md` | Mevcut, 128 satır — input/output/exit code/allowlist/normalization/fail-closed/V0 devir/recovery immutability |

## Acceptance evidence

### 1. Test: pytest tests/Architecture/TaskScope

80 passed (65.72s) — transcript `test-fullsuite.txt` (tüm TaskScope testleri yeşil).

### 2. Test: pytest test_task_scope_diff.py özel

9/9 passed (7.09s):
- TestDiffMode: committed out-of-scope ✗, committed in-scope ✓, clean-worktree with bad commit ✗, worktree-only ignored, rename both paths checked ✗
- TestAllowlistShapeFilter: task-id fragment ✗, prose fragment ✗, path-shaped ✓, dotted filename ✓

### 3. Local preflight

`py tools/task-scope/task_scope_tool.py --task-id V1-FND-003 --format text` -> `OK: All changes within scope for V1-FND-003`, exit 0

### 4. CI workflow

`.github/workflows/task-scope.yml` — PR trigger + workflow_dispatch (manual Task ID override), diff-base modu, windows-latest runner, pwsh. Local ve CI aynı exit code/fixture contract'ı (contract.md'de belgeli).

### 5. Contract

`docs/engineering/task-scope-contract.md` — 128 satır, tüm fail-closed durumları, path normalization (backslash->slash, lowercase, `./` stripping, `..` traversal rejection), glob (`**`/`*`/`?`), allowlist shape filter, worktree+diff modları, C44 V0 devir istisnası, failure recovery, task Markdown immutability.

## Test: dotnet test ALKAROS.slnx --no-build

Geçer: exit 0 (tam çözüm — V0 kapanış koşusu, 279/279).

## Kanıt

Bu görev yalnız mevcut dosyaları doğrulamış, hiçbir owned surface dosyasında yazma yapmamıştır (hepsi zaten mevcuttu). V0-GOV-031/032 araç/test geliştirmeleri yapmış; V1-FND-003 bu çalışmanın acceptance'ını doğrulayarak kapatmıştır.

## Sonuç

Tüm acceptance kriterleri karşılanmıştır. Görev Done.
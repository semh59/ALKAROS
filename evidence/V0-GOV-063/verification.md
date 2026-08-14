# V0-GOV-063 - Verification record (C66)

Date: 2026-08-14
Branch: codex/audit-remediation
Workspace: D:\PROJECT\ALKAROS-REMEDIATION

## 1. plan validator

Command:
python -B tools/plan-audit/plan_audit_tool.py validate

Output (tail):
Markdown files: 373
Task files: 352
Registered gates: 18
Registered EXT sources: 21
Dependency edges: 1236
Validation errors: 0
Validation warnings: 0

Exit code: 0

## 2. TaskScope + PlanAudit test suites

Command:
python -m pytest tests/Architecture/TaskScope tests/Architecture/PlanAudit -q

Output (tail):
152 passed in 219.82s (0:03:39)

Exit code: 0

Command:
python -m pytest tests/Architecture/TaskScope/test_task_scope.py -q

Output (tail):
102 passed in 107.76s (0:01:47)

Exit code: 0

## 3. Trailing whitespace / line-ending check

Command:
git diff --check

Output: clean (no output)

Exit code: 0

## 4. C54 admission behavior (fail-closed)

- Deferred olmayan Blocked V0 görevi (V0-GOV-040 Planned -> Blocked) varken:
  APPLICATION_STARTED_BEFORE_V0_EXIT V1-FND-022 üretilir
  (test_other_v1_application_is_not_admitted, PlanAudit suite).
- Tüm Blocked V0 görevleri deferred iken C54 desen/source/authority/dependencies
  mutasyonları C54_APPLICATION_ADMISSION_* hatalarıyla yakalanır
  (test_c54_admission_divergence_fails_closed, 6 parametrize case).
- plan_audit_tool.py application_tasks_started_before_v0_exit: v0_gate_open=False
  iken c54_errors doğrudan döner (eski davranış: [] dönüyordu, mutasyonlar
  sessizce geçiyordu).

## 5. Added-task-Markdown baseline fix (user-approved scope)

Kullanıcı onaylı kapsam düzenlemesi (2026-08-14): task_scope_tool.py
validate_task_markdown_change diff-mode'da merge-base'de görev dosyası yoksa
(added) baseline olarak HEAD'deki dosyayı kullanır; worktree modunda
HEAD'de de yoksa eski fail-closed davranışı korunur
(test_untracked_task_cannot_supply_its_own_allowlist hâlâ geçer).

Regression test PR #2 task-scope gate'i için ilk commit'te test_task_scope_diff.py'ye
eklendi; V1-FND-003'ün dosyasına dokunmamak için test, V0-GOV-063'ün sahibi
olduğu test_task_scope.py'ye taşındı (TestAddedTaskMarkdownDiffMode).

Command:
python -m pytest tests/Architecture/TaskScope -q
python -m pytest tests/Architecture/TaskScope tests/Architecture/PlanAudit -q

Output (tail):
1 passed (added_task_markdown)
130 passed in 116.00s (0:01:55)
153 passed in 193.58s (0:03:13)

Exit code: 0

## 6. Write set (this task)

M plan/GATES.md
M plan/TRACEABILITY.md
M plan/VALIDATION_CONTRACT.md
M plan/v0/governance/V0-GOV-060-v3-b0-blob-alignment.md (yalnız yüzey devir notu, CORR:C66)
M plan/v0/governance/V0-GOV-062-rev-deferral.md (yalnız yüzey devir notu, CORR:C66)
M plan/v1/identity-authorization/V1-IAM-008-authorization-linearization.md
M tests/Architecture/PlanAudit/test_plan_audit.py
M tests/Architecture/TaskScope/test_task_scope.py
(TestAddedTaskMarkdownDiffMode, tip test_task_scope_diff.py'den taşındı)
M tools/plan-audit/plan_audit_tool.py
M tools/task-scope/task_scope_tool.py
?? plan/v0/governance/V0-GOV-063-defer-gov-041-042.md
?? evidence/V0-GOV-041/ruleset-readback.json (V0-GOV-041 kanıtı, committed değil)

## 6. Handoff

- V1-IAM-008 (blocker GOV-041/042 kalktı, C66 kaydı Blocker bölümünde)
- V0-GOV-045

## 7. PR #3 merge + ruleset context fix (user-approved scope)

PR #2 task-scope check geçti (2026-08-14, run 31775223010 pass), ancak GitHub merge
engelledi: `mergeable_state=blocked`. Kök neden: ruleset verify-required-checks (id
20817586) required_status_checks context'i `task-scope / Task scope enforcement`
bekliyordu; workflow check-run adı ise `Task scope enforcement` (job name). GitHub
check-run'ları name ile eşleştirdiğinden beklenti karşılanmıyor, kural "expected ama
perform edilmedi" sayılıyordu. Kullanıcı onayı ile ruleset context'i `Task scope
enforcement` olarak güncellendi (PUT /repos/semh59/ALKAROS/rulesets/20817586,
updated_at 2026-08-14T13:52:38.069+03:00); strict policy korundu.

PR #3 (V0-GOV-063: Defer V0-GOV-041/042 integration gates (C66)):
https://github.com/semh59/ALKAROS/pull/3 — merged, merge commit 551d75d
(2026-08-14T10:53:20Z).

Komut kanıtı:

Command:
gh pr checks 3 → Task scope enforcement pass
gh api repos/semh59/ALKAROS/pulls/3 --jq .mergeable_state → clean (ruleset fix sonrası)
git log origin/master → 551d75d Merge pull request #3

## 8. PR #5 close: InProgress -> Done transition support (user-approved scope)

PR'da `Done` durumu task-scope tarafından reddediliyordu (EXECUTABLE_STATUSES =
Planned|InProgress); V0-GOV-063'ü kapatmak imkânsızdı. Kullanıcı onayı ile
(2026-08-14) task_scope_tool.py'ye yalnız InProgress->Done geçişine izin veren
`allow_done_transition` istisnası eklendi (`_is_legal_done_transition`:
current Done + baseline InProgress; Planned/Blocked->Done reddedilir). Diff-mode
baseline çözümüne de merge-base'de görev dosyası yoksa HEAD fallback'i eklendi
(validate_task_markdown_change ile tutarlı).

Testler (tests/Architecture/TaskScope/test_task_scope.py,
TestDoneTransitionDiffMode):
- test_done_transition_from_in_progress_accepted -> pass
- test_done_transition_from_planned_rejected -> pass
- test_added_task_markdown_in_diff_mode_uses_head_baseline -> pass

Command:
python -m pytest tests/Architecture/TaskScope tests/Architecture/PlanAudit -q
python -B tools/plan-audit/plan_audit_tool.py validate

Output (tail):
155 passed in 223.66s (0:03:43)
Validation errors: 0 / Validation warnings: 0

Exit code: 0

PR #5 (V0-GOV-063: Close task - Done transition support (C66)):
https://github.com/semh59/ALKAROS/pull/5 — merged, merge commit c5ab8e5
(2026-08-14T12:35:58Z). V0-GOV-063 Status: Done (master'da).

Komut kanıtı:
gh pr checks 5 → Task scope enforcement pass
gh api repos/semh59/ALKAROS/pulls/5 --jq .mergeable_state → clean
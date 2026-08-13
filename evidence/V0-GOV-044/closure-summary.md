# V0-GOV-044 closure summary — markdownlint remediation

- Tarih: 2026-08-13
- Repo: `D:\PROJECT\ALKAROS-REMEDIATION`
- Branch: `codex/audit-remediation`
- Görev: `V0-GOV-044` (CORR:C52) — user scope extension onayı: "hepsini düzelt"
- Plan değişikliği commit: `5419e7f` (`docs(v0-gov-044): extend owned surface to all 74 lint-failing paths`)

## Başlangıç durumu (canlı ölçüm, 2026-08-13)

`npx markdownlint-cli2@0.23.2 plan/** docs/** evidence/** AGENTS.md` (config `.markdownlint-cli2.jsonc`):

- Toplam hata: **281**
- Etkilenen dosya: **74**
- Kural dağılımı: MD013 ×120, MD060 ×66, MD040 ×46, MD032 ×12, MD047 ×9,
  MD004 ×7, MD036 ×6, MD031 ×5, MD022 ×2, MD038 ×2, MD058 ×1, MD056 ×1,
  MD009 ×1, MD033 ×1, MD025 ×1, MD034 ×1.

## Uygulanan remediasyon

1. `markdownlint-cli2 --fix` — otomatik düzeltilebilir kurallar (MD060, MD032,
   MD047, MD004, MD031, MD022, MD038, MD058, MD009, MD034).
2. MD013 line-length — kelime sınırından 120 karaktere sarma scripti
   (`md013_wrap.py`); başlık, tablo ve fenced-code satırları korundu; tablo
   satırlarının yanlışlıkla sarılması deneme aşamasında tespit edilip HEAD'e
   dönüşle geri alındı ve script fence/tablo bilinçli hale getirildi.
3. MD040 — 46 adet dilsiz fence bloğuna `console` dili eklendi (içeriklerin
   tamamı command transcript; `md040_fix.py` state-machine ile yalnız açılış
   fence'leri etiketledi).
4. MD036 ×6 — `**"..."**` emphasis-as-heading satırları düz metne çevrildi
   (anlam korundu, heading dönüşümü yapılmadı).
5. MD025 — `V1-IAM-002/closure-2026-08-08.md` ikinci `#` başlığı `##` yapıldı.
6. MD033 — `ENV-003/env003-test-matrix.md` `<proj>` inline HTML'si backtick'e
   alındı.
7. MD056 — `plan/TRACEABILITY.md` C46 satırında hücre içi literal pipe'lar
   (`{TableLifecycle | TableTransfer | ...}`) `\|` ile escape edildi (tablo
   9 sütuna bölünüyordu).

## Semantic doğrulama

- Token-diff (`token_diff_check.py`): HEAD vs worktree — 73 dosyada tek token
  farkı `console` fence etiketi; hash, exit code, tarih, test sayısı içeren
  hiçbir iddia değişmedi.
- Diff review: yalnız whitespace/sarma/fence/başlık seviyesi/escape değişiklikleri.

## Acceptance evidence (exit code'lar)

- `markdownlint-cli2` (tüm globs): **0 issues in 0 files**, exit 0 — `markdownlint_final.txt`
- `python -B tools/plan-audit/plan_audit_tool.py validate`: 0 error / 0 warning, exit 0 — `plan_audit_validate.txt`
- `task_scope_tool.py --task-id V0-GOV-044`: metadata_errors 0; kalan 8 finding **tamamı V0-GOV-058'e ait**
  (başka oturumun commit'siz işi: `plan/v0/governance/V0-GOV-058-dynamic-route-parity.md`,
  `tests/Architecture/TaskScope/test_task_scope.py`, `evidence/V0-GOV-058/**`);
  V0-GOV-044 kapsamında sıfır finding — `task_scope_worktree.json`

Not: V0-GOV-058 dosyaları bu görevin allowlist'i dışında olup başka bir Codex
oturumu tarafından eşzamanlı yürütülmektedir (AGENTS.md gereği dokunulmadı).
TaskScope `valid: false` yalnız bu dış oturumun commit'siz durumundan kaynaklanır;
V0-GOV-044 owned surface kapsamındaki tüm değişiklikler allowlist'e uygundur.

## Kapanış

- `V0-GOV-044` metadata: Status `Done` (2026-08-13), Assignee `/root/implement_v0_gov_044`.
- Kalan lint borcu: **0** — repo tamamı yeşil.

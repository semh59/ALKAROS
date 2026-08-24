# V1-REM-001 - Orphan remediation evidence reconciliation

- Task ID: V1-REM-001
- Status: Done
- Assignee: Codex-/root
- Work type: validation
- Surface state: Existing

## Goal

Task tanımı olmadan bırakılan `V1-REM-001` kanıtını mevcut repository gerçeğiyle uzlaştırmak; eski SDK,
commit ve write-set iddialarını kaldırıp manifest girdisini izlenebilir hale getirmek.

## Owned surface

- `evidence/V1-REM-001/**`

## In scope

- Mevcut kanıt metnindeki doğrulanamayan veya artık geçerli olmayan iddiaları repository gerçeğiyle değiştirmek.
- Kanıtı yalnız yeniden üretilebilir komut ve commit kimlikleriyle sınırlamak.

## Out of scope

- Uygulama, migration, test, build configuration, audit manifesti veya gate kapanış dosyalarını değiştirmek.
- Mevcut kullanıcı değişikliklerini sahiplenmek, silmek ya da geri almak.

## Dependencies

- V1-RMD-002

## Acceptance evidence

- `evidence/V1-REM-001/remediation_evidence.md` yalnız mevcut Git geçmişi ve canlı doğrulama sonuçlarıyla
  desteklenen ifadeler içerir.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit 0 verir.
- `python -B tools/task-scope/task_scope_tool.py --task-id V1-REM-001 --format text` exit 0 verir.
- Semih, kanıttaki commit kimliklerini `git show --stat <commit>` ile yerel olarak doğrulayabilir.

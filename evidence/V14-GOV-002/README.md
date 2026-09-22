# V14-GOV-002 - Kanıt özeti

Semih'in 2026-09-22 onayıyla `GATE-V14-ENTRY`'nin `GATE-V13-EXIT`e bağımlılığı
kaldırıldı (`V14-GOV-001`'in 2026-09-18 sıralama kararının geri alınması).

## Bulunan ve düzeltilen ayrı kusur

`tools/task-scope/task_scope_tool.py`'nin `_DEFERRED_TASK_RECORDS` sabiti
(V0 deferral mekanizması) `plan/GATES.md`'nin gerçek `V0_DEFERRED_TASKS`
tablosuyla eşleşmiyordu (6 satırda `reopen_stage`/`required_evidence` farkı).
`parse_v0_deferral_ids(Path('plan'))` bu yüzden gerçek plan dizinine karşı
her zaman `TaskParseError` fırlatıyordu — aynı oturumda düzeltildi.

## Dosyalar

- `pytest-task-scope.txt` — `tests/Architecture/TaskScope/` tam koşusu, 137/137.
- `plan-audit-validate.txt` — `plan_audit_tool.py validate`, 0 hata/0 uyarı.
- `consistency-audit.txt` — `consistency_audit.py`, temiz.

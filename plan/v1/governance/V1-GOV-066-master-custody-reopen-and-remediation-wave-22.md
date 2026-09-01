# V1-GOV-066 - Master custody reopen and remediation wave 22

- Task ID: V1-GOV-066
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: governance
- Surface state: Planned

## Source basis

- PO:2026-09-01

## Goal

Semih onayıyla (2026-09-01), restoran POS rakiplerinin (Toast, Square for Restaurants, Lightspeed Restaurant, Oracle MICROS Simphony, Türk pazarı) fiili taahhütleri araştırılıp `V0-BKP-002` RPO/RTO hedefleri rekabete göre kalibre edilir ve PostgreSQL WAL arşivleme (point-in-time recovery) V1'e çekilir: fiziksel base backup + sürekli WAL arşivi + hedef zamana geri oynatma, para/mali/denetim verisi için ~5 dakikalık RPO sağlar. `GATE-V1-EXIT` kapısı yeniden açılır; 1 kurtarma görevi (`V1-RMD-095`) ve kapanış görevi (`V1-GOV-067`) planlanır. Kalibre hedefler ve WAL mekanizmasıyla `V0-BKP-001` ve `V0-BKP-002` devir listesinden çıkarılıp kapanır.

## Owned surface

- `plan/v1/governance/V1-GOV-066-master-custody-reopen-and-remediation-wave-22.md`
- `plan/v1/remediation/V1-RMD-095-wal-archiving-point-in-time-recovery.md`
- `plan/v1/governance/V1-GOV-067-wave22-master-audit-reseal-and-gate-closure.md`
- `plan/GATES.md`
- `plan/v1/README.md`
- `plan/TRACEABILITY.md`
- `plan/v0/backup-recovery/V0-BKP-001-backup-restore-proof.md`
- `plan/v0/backup-recovery/V0-BKP-002-rpo-rto-targets.md`
- `tools/plan-audit/plan_audit_tool.py`
- `tools/task-scope/task_scope_tool.py`
- `tests/Architecture/TaskScope/test_task_scope.py`
- `evidence/V1-GOV-066/**`

## In scope

- `GATE-V1-EXIT` kapısını `plan/GATES.md` ve `plan/v1/README.md` üzerinde yeniden açılmış olarak güncellemek.
- V1 görev matrisini 22. dalga görevleriyle (`V1-RMD-095`, `V1-GOV-067`) genişletmek ve sayımı güncellemek.
- Yüzey devri: `docs/recovery/rpo-rto-targets.md` yüzeyi `V0-BKP-002`'den `V1-RMD-095`'e devredilir.
- `V0-BKP-001` ve `V0-BKP-002` görevlerini `## Onay` bloklu `Done` yapmak (C69 emsali); `## Blocker` bölümlerini kaldırmak; `Assignee`'yi `Semih (product owner)` yapmak.
- `V0-BKP-001`/`V0-BKP-002` satırlarını `plan/GATES.md` `V0_DEFERRED_TASKS` tablosundan, `plan_audit_tool.py` `V0_DEFERRED_TASKS` setinden, `task_scope_tool.py` `_DEFERRED_TASK_RECORDS` setinden ve `tests/Architecture/TaskScope/test_task_scope.py` `DEFERRED_TASK_IDS`/`DEFERRED_ROWS` listelerinden senkron çıkarmak; aktif deferral kümesi 11 → 9 görev.
- `plan/TRACEABILITY.md`'ye C73 kaydı eklemek.
- `python tools/plan-audit/plan_audit_tool.py validate` komutunu sıfır hata ve sıfır uyarı ile çalıştırmak.

## Out of scope

- Çoklu node / warm standby streaming replikasyon (`V15-BKP` kapsamı); bu iş tek node fiziksel base backup + WAL arşivi sağlar.
- Şifreli tesis dışı otomasyon ve zamanlanmış resumable restore drill'i (`V15-BKP-001`, `V15-BKP-002`, `V20-DRL-001`).
- Uygulama kodu davranışı; bu iş yalnız PostgreSQL konfigürasyonu, operatör betikleri ve dokümandır.

## Dependencies

- V1-GOV-065

## Deliverables

- Yeniden açılmış `GATE-V1-EXIT` kapı dokümanı ve güncel görev matrisi.
- Sıralı kurtarma görevi (`V1-RMD-095`) ve kapanış görevi (`V1-GOV-067`).
- `Done` `V0-BKP-001`/`V0-BKP-002` ve 9 görevlik güncel deferral kümesi.

## Acceptance evidence

- `python tools/plan-audit/plan_audit_tool.py validate` sıfır hata ve sıfır uyarı verir.
- `python -m pytest tests/Architecture/TaskScope tests/Architecture/PlanAudit` sıfır hata verir (güncel 9 görevlik deferral kümesi).
- `GATE-V1-EXIT` kapısı `plan/GATES.md` ve `plan/v1/README.md` üzerinde açık olarak belgelenir.

## Handoff

- V1-RMD-095

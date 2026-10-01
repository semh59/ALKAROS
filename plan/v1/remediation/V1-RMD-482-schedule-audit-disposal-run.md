# V1-RMD-482 - Denetim bölümü bırakma komutunun düzenli çalıştırılması

- Task ID: V1-RMD-482
- Status: Done
- Assignee: claude-code-session_01XpoF59o3sDPfb7ZADR4BMf
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

`V1-RMD-481` ile gelen `audit-disposal` komutu 10 yılı dolan denetim bölümünü bırakır. KVKK imha aralığı en çok 6 aydır; komutun düzenli (en az altı ayda bir)
çalışması ve sonucunun izlenmesi için çalışma takvimi ve uyarı yolu bu görevde belirlenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-482-schedule-audit-disposal-run.md`
- `evidence/V1-RMD-482/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): compose.ops.yaml — yalnız `audit-disposal` servisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Deployment/test_container_contract.py — yalnız bu servisin sözleşme testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/compliance/kvkk-retention-runbook.md — yalnız denetim kaydı bölümü
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/operations/production-go-live-checklist.md — yalnız bir kontrol satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `compose.ops.yaml` içinde `ops` profilindeki `audit-disposal` servisi (`housekeeping` ile aynı kalıp): komutu `--apply` ile çalıştırır, çıktı satırı (`dropped=`, `default_partition_rows=`) günlükte kalır.
- Dağıtım sözleşme testi, runbook'ta altı ayı aşmayan cron satırı ve go-live kontrol listesinde bir madde.

## Out of scope

- Komutun kendisi (`V1-RMD-481`).

## Dependencies

- V1-RMD-481

## Acceptance evidence

- Kanıt `evidence/V1-RMD-482/` altındadır.

## Handoff

- None

# V12-RMD-010 - Sorunlar sekmesi uç nokta dosyasının sahipliği

- Task ID: V12-RMD-010
- Status: InProgress
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: remediation
- Surface state: Existing

## Goal

`V12-OUI-006` (`8d69e7d6`) yeni bir üretim dosyası olan
`src/Host/Experience/Reconciliation/OnlineProblemsEndpoints.cs`'yi yalnız "Sınırlı ek" dizini üzerinden yazdı; o
bildirim yazma izni verir, sahiplik vermez. Plan denetimi `UNOWNED_PRODUCTION_FILE` hatası verdi ve commit bu
hatayla master'a gönderildi (kapanış zincirinde hata satırı açıkça eşlenmedi). Bu görev dosyanın sahibidir.

## Owned surface

- `src/Host/Experience/Reconciliation/OnlineProblemsEndpoints.cs`
- `evidence/V12-RMD-010/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Dosyanın sahipliği; içerik değişmez.
2. Hatanın ve nedeninin kanıta yazılması.

## Out of scope

- Dosyanın davranışı (`V12-OUI-006`'da test ve mutasyonla kanıtlı).

## Dependencies

- V12-OUI-006

## Acceptance evidence

- `plan_audit_tool.py validate` "Validation errors: 0" satırı açıkça eşlenerek; `consistency_audit.py` temiz.
- `task_scope_tool.py --task-id V12-RMD-010 --diff-base <InProgress commit>` exit 0.

## Handoff

- None

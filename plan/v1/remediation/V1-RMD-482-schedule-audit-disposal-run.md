# V1-RMD-482 - Denetim bölümü bırakma komutunun düzenli çalıştırılması

- Task ID: V1-RMD-482
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

`V1-RMD-481` ile gelen `audit-disposal` komutu 10 yılı dolan denetim bölümünü bırakır. KVKK imha aralığı en çok 6 aydır; komutun düzenli (en az altı ayda bir)
çalışması ve sonucunun izlenmesi için çalışma takvimi ve uyarı yolu bu görevde belirlenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-482-schedule-audit-disposal-run.md`

## In scope

- Kesin yollar görev başlatılırken yazılır. `audit-disposal` komutunun çıktısı (`dropped=`, `default_partition_rows=`) izlenir; çalışmazsa uyarı üretilir.

## Out of scope

- Komutun kendisi (`V1-RMD-481`).

## Dependencies

- V1-RMD-481

## Acceptance evidence

- Kanıt `evidence/V1-RMD-482/` altındadır.

## Handoff

- None

# V1-RMD-563 - Veritabanı içgörüsünün Host işi

- Task ID: V1-RMD-563
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

V1-RMD-546 veritabanı içgörüsü 60 sn periyodik Host işiyle çalışır ve bulguları hata olayı olarak yazıcıya verir.

## Owned surface

- `plan/v1/remediation/V1-RMD-563-insight-runner.md`
- `evidence/V1-RMD-563/**`
- `src/Host/Observability/InsightRunner/**`
- `tests/Host/Observability/InsightRunner/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız iş kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- İzleme havuzuyla periyodik okuma; bulgular yazıcıya verilir; bir tur hata verirse döngü sürer ve hata sayılır.

## Out of scope

- N+1, EXPLAIN, kaçan arka plan işi (V1-RMD-558).

## Dependencies

- V1-RMD-546
- V1-RMD-534

## Acceptance evidence

1. Gerçek kilit zinciri bir turda `db.lock_wait` olayı olarak yazıcıdan veritabanına düşer.
2. Bir tur hata verirse sonraki tur çalışır.
3. Çıktılar `evidence/V1-RMD-563/` altındadır.

## Handoff

- None

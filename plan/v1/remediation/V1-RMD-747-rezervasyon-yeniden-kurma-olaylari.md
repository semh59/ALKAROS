# V1-RMD-747 - Rezervasyon bakiyesi yeniden kurulunca uygulanmış olay kayıtları siliniyor ve geri yazılmıyor (739 bulgusu V11-INV-007)

- Task ID: V1-RMD-747
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-28

## Goal

RebuildReservationBalancesAsync reservation_balance_applied_events tablosunu siler ama yeniden doldurmaz; RecordAppliedEventsForRebuildAsync hiç çağrılmaz. Yeniden kurmadan sonra, zaten bırakılmış/tüketilmiş bir rezervasyonun terminal geçişi tekrar oynatılırsa idempotency kaydı bulunamaz ve bakiye ikinci kez düşer. Düzeltme: silme ve yeniden yazma tek işlemde: her rezervasyon için 'Reserved' olayı, terminal durumdakiler için ayrıca 'Terminal' olayı portion_reservations tablosundan veritabanında üretilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-747-rezervasyon-yeniden-kurma-olaylari.md`
- `evidence/V1-RMD-747/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Inventory/ReservationBalanceProjection/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Inventory/ReservationBalanceProjection/**
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Inventory/PortionReservations/**
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Silme ve olay üretimini tek depo yönteminde birleştirmek (kullanılmayan RecordAppliedEventsForRebuildAsync kaldırılır)
- Yeniden kurma sonrası terminal geçiş tekrarının idempotent kaldığını gerçek PostgreSQL ile kanıtlayan test

## Out of scope

- Rezervasyon yaşam döngüsünün başka davranışları

## Dependencies

- V1-RMD-737

## Acceptance evidence

1. Yeni test kırmızıdan yeşile döner; günlük gerçek exit code ile `evidence/V1-RMD-747/` altındadır.
2. Mutasyon kanıtı: düzeltme geri alınınca test kırılır (`mutations.log`, kaynak geri yüklenmiş).
3. `plan_audit_tool.py validate` ve `consistency_audit.py` sıfır hata ve sıfır uyarı verir; `task_scope_tool.py` exit 0 döner; çıktılar `closing/` altındadır.

## Handoff

- V1-RMD-739

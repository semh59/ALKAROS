# V1-RMD-536 - Akıllı şiddet ve izleyicinin izlenmesi

- Task ID: V1-RMD-536
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Bir hata normalden çok sıklaşırsa şiddeti otomatik yükselir (saat dilimi ve hacim farkındalığıyla); dedektörler çalışmazsa bu da bir alarmdır.

## Owned surface

- `plan/v1/remediation/V1-RMD-536-smart-severity-and-self-watch.md`
- `evidence/V1-RMD-536/**`
- `src/Modules/Observability/ErrorAnalysis/**`
- `tests/Modules/Observability/ErrorAnalysis/**`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Z-skor, aynı saat dilimi ve hafta günü tabanıyla; en az örnek ve en az mutlak sayı koşulu; sıfıra yakın seride tek olay alarm vermez.
- Dedektör nabız sözleşmesi: her dedektör son çalışma zamanını yazar; eşik aşılınca `detector_degraded` olayı; açılışta kendi kendine test.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-533

## Acceptance evidence

1. Açılış saatinde (düşük taban) tek hata yükseltme üretmez.
2. Taban 10 kat aşılınca yükseltme 5 dakika içinde olur.
3. Dedektör durdurulunca 2 nabız süresi içinde `detector_degraded` olayı.
4. Çıktılar `evidence/V1-RMD-536/` altındadır.

## Handoff

- None

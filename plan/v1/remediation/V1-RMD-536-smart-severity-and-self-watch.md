# V1-RMD-536 - Akıllı şiddet ve izleyicinin izlenmesi

- Task ID: V1-RMD-536
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: New

## Source basis

- PO:2026-10-15

## Goal

Bir hata normalden çok sıklaşırsa şiddeti otomatik yükselir; dedektörler çalışmazsa bu da bir alarmdır.

## Owned surface

- `plan/v1/remediation/V1-RMD-536-smart-severity-and-self-watch.md`
- `evidence/V1-RMD-536/**`
- `src/Modules/Observability/ErrorEvents/Analysis/**`
- `tests/Modules/Observability/ErrorEvents/Analysis/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Saatlik sayaçlardan Z-skor (> 3 ve en az 6 örnek) ile CRITICAL'e yükseltme.
- Açılışta kendi kendine test; her dedektörün son çalışma zamanı ve `detector_degraded` olayı.

## Out of scope

- Bildirim.

## Dependencies

- V1-RMD-533

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; gerçek Postgres testi; çıktılar `evidence/V1-RMD-536/` altındadır.

## Handoff

- None

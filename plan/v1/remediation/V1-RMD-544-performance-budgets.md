# V1-RMD-544 - Performans bütçeleri ve gerileme alarmı

- Task ID: V1-RMD-544
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Her kritik eylemin (sipariş onayla, ödeme al, fiş yazdır, mutfağa ilet) süre bütçesi vardır; bütçe aşılınca veya son 7 günün normaline göre yavaşlayınca alarm üretilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-544-performance-budgets.md`
- `evidence/V1-RMD-544/**`
- `src/Modules/Observability/PerformanceBudgets/**`
- `tests/Modules/Observability/PerformanceBudgets/**`
- `database/migrations/V1/V1-RMD-544/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Eylem başına bütçe (ör. p95 800 ms) ve taban çizgisi karşılaştırması (son 7 günün aynı saat dilimi).
- Gerileme: aynı eylem yeni sürümden sonra belirgin yavaşladıysa alarm.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-543

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; gerçek Postgres testi; çıktılar `evidence/V1-RMD-544/` altındadır.

## Handoff

- None

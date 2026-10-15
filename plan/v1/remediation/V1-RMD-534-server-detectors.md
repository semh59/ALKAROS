# V1-RMD-534 - Sunucu dedektörleri

- Task ID: V1-RMD-534
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Yakalanmamış istisna, yavaş istek, yavaş sorgu, bağlantı havuzu doluluğu, uzun işlem/kilit ve kaçan arka plan işi hata olayı olarak yakalanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-534-server-detectors.md`
- `evidence/V1-RMD-534/**`
- `src/Host/Observability/ServerDetectors/**`
- `tests/Host/Observability/ServerDetectors/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Global istisna yakalayıcı (CRITICAL), istek süresi eşiği, sorgu süresi eşiği (EXPLAIN planı ile), havuz > %85, işlem > 30 sn, zamanlanmış iş kaçırma.
- Eşikler ayarlanabilir yapılandırmadan okunur.

## Out of scope

- Alan dedektörleri; bildirim.

## Dependencies

- V1-RMD-533

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; gerçek Postgres testi; çıktılar `evidence/V1-RMD-534/` altındadır.

## Handoff

- None

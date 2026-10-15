# V1-RMD-535 - Alan dedektörleri

- Task ID: V1-RMD-535
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: New

## Source basis

- PO:2026-10-15

## Goal

Yazıcı kopması/tekrar denemesi, Bilinmeyen ödeme sonucu, mutfağa iletilmeyen sipariş, Yemeksepeti/QNB/Trendyol Go entegrasyon hataları, yedek ve disk durumu hata olayı olarak yakalanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-535-domain-detectors.md`
- `evidence/V1-RMD-535/**`
- `src/Host/Observability/DomainDetectors/**`
- `tests/Host/Observability/DomainDetectors/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Her kaynağın mevcut olay/durum kayıtlarından okuyan dedektör; kaynak modülün kodu değişmez.
- Bir hata türü için tanımlı eşik aşılınca olay açılır, durum düzelince otomatik çözülür.

## Out of scope

- Sunucu dedektörleri; bildirim.

## Dependencies

- V1-RMD-533

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; gerçek Postgres testi; çıktılar `evidence/V1-RMD-535/` altındadır.

## Handoff

- None

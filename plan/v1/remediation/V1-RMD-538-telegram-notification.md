# V1-RMD-538 - Telegram bildirimi ve özet

- Task ID: V1-RMD-538
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Kritik hata anında, diğer hatalar 5 dakikalık özetle Telegram'a gider; aynı hata için tekrar bildirimi bastırılır; gönderilemeyen bildirim kaybolmaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-538-telegram-notification.md`
- `evidence/V1-RMD-538/**`
- `src/Modules/Observability/ErrorNotifications/**`
- `tests/Modules/Observability/ErrorNotifications/**`
- `database/migrations/V1/V1-RMD-538/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Bildirim kuyruğu (outbox) Postgres'te; 3 deneme, sonra bekletme; tekrar bastırma süresi veritabanında.
- Bot anahtarı ve sohbet kimliği gizli bilgi sağlayıcısından okunur; yapılandırılmamışsa kanal kapalıdır ve ekranda bu belirtilir.

## Out of scope

- Gerçek Telegram hesabı doğrulaması (dış hesap gerekir).

## Dependencies

- V1-RMD-533

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; gerçek Postgres testi; çıktılar `evidence/V1-RMD-538/` altındadır.

## Handoff

- None

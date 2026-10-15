# V1-RMD-557 - Telegram gerçek doğrulama (dış hesap)

- Task ID: V1-RMD-557
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: validation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Gerçek Telegram bot hesabıyla kritik olayın ve özetin teslimi doğrulanır; dış hesap gelene kadar bekler.

## Owned surface

- `plan/v1/remediation/V1-RMD-557-telegram-live-verification.md`
- `evidence/V1-RMD-557/**`
- `evidence/V1-RMD-557/**`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Gerçek bot ile test mesajı, kritik olay, özet ve kanal kapalı davranışının doğrulaması.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-538

## Acceptance evidence

1. Gerçek hesaptan alınan 3 mesajın kanıtı (künye biçimi).
2. Bot anahtarı kanıt dosyalarında yer almaz.
3. Çıktılar `evidence/V1-RMD-557/` altındadır.

## Handoff

- None

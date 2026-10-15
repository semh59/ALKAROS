# V1-RMD-561 - Bildirim teslimatının Host bağlantısı

- Task ID: V1-RMD-561
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Hata olayları bildirim kuyruğuna girer ve dağıtıcı arka plan işi olarak çalışır; Telegram anahtarı yapılandırılmamışsa kanal kapalı kalır.

## Owned surface

- `plan/v1/remediation/V1-RMD-561-notification-delivery-wiring.md`
- `evidence/V1-RMD-561/**`
- `src/Host/Observability/NotificationWiring/**`
- `tests/Host/Observability/NotificationWiring/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Observability/ServerDetectors/ServerDetectors.cs - yalnız birleşik olay alıcısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız teslimat kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Birleşik olay alıcısı (uyarı köprüsü + bildirim kuyruğu); 30 sn dağıtıcı işi; Telegram bilgisi yapılandırmadan (Monitoring:Telegram:BotKey, ChatId) okunur, yoksa kanal kapalı.

## Out of scope

- Gerçek Telegram gönderimi, ekranda kanal durumu ve HTTP istemci günlük bastırması (V1-RMD-557, V1-RMD-539); çöküş kanalının bağlanması.

## Dependencies

- V1-RMD-538
- V1-RMD-534

## Acceptance evidence

1. Yazıcıya düşen Kritik olay kuyruğa girer ve dağıtıcı tek mesaj gönderir (gerçek Postgres).
2. Alıcılardan biri hata verse diğeri yine çağrılır; yapılandırma yoksa kanal kapalıdır.
3. Çıktılar `evidence/V1-RMD-561/` altındadır.

## Handoff

- None

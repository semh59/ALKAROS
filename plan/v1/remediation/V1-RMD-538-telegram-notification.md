# V1-RMD-538 - Bildirim kuyruğu ve Telegram kanalı (iç)

- Task ID: V1-RMD-538
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Kritik hata anında, diğerleri 5 dakikalık özetle bildirim kanalına gider; Telegram, V15-NOT-001 kanal sözleşmesinin bir kanalıdır; yalnız sabit künye gönderilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-538-telegram-notification.md`
- `evidence/V1-RMD-538/**`
- `src/Modules/Observability/ErrorNotifications/**`
- `tests/Modules/Observability/ErrorNotifications/**`
- `database/migrations/V1/V1-RMD-538/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Bildirim kuyruğu Postgres'te; 3 deneme; tekrar bastırma süresi veritabanında; 429 yanıtında bekleme; internet dönünce eski bildirim seli tek özete indirilir.
- Bot anahtarı ve sohbet kimliği gizli bilgi sağlayıcısından okunur; adres günlüğe yazılmaz (HTTP istemci günlüğü bastırılır); yapılandırılmamışsa kanal kapalıdır ve ekranda belirtilir.
- İçerik yalnız sabit künye: kategori, sayı, saat, terminal etiketi.

## Out of scope

- Gerçek Telegram doğrulaması (V1-RMD-557).

## Dependencies

- V1-RMD-533
- V1-RMD-549

## Acceptance evidence

1. Kritik olay kuyruğa 5 sn içinde girer; gönderim ilk denemede 60 sn içinde.
2. Bot anahtarı günlüklerde ve hata metninde 0 kez geçer (test).
3. 100 birikmiş bildirim 1 özet mesajı olur.
4. Çıktılar `evidence/V1-RMD-538/` altındadır.

## Handoff

- None

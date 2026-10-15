# V1-RMD-541 - Arayüz etkileşim izleme

- Task ID: V1-RMD-541
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Kasa, garson ve mutfak ekranlarında basılan her düğme, sayfa değişimi ve form gönderimi bir etkileşim olayı olarak kaydedilir: ne, ne zaman, kim (rol/terminal), ne kadar sürdü, sonucu ne oldu.

## Owned surface

- `plan/v1/remediation/V1-RMD-541-ui-interaction-telemetry.md`
- `evidence/V1-RMD-541/**`
- `src/Clients/PosTerminal/src/features/interaction-telemetry/**`
- `src/Host/Observability/InteractionTelemetry/**`
- `tests/Host/Observability/InteractionTelemetry/**`
- `database/migrations/V1/V1-RMD-541/**`
- Bu görev, başka bir görevin owned surface alanını değiştiremez.

## In scope

- Otomatik izleme: her düğme, bağlantı ve form için tek noktadan olay; element kimliği `data-telemetry-id` veya erişilebilir ad anahtarıdır, görünen metin veya girilen değer değildir.
- Her etkileşim için süre: dokunuştan ekranın güncellenmesine kadar (INP benzeri), uzun görev (50 ms üstü donma) ve düzen kayması.
- Toplu gönderim (`sendBeacon`), çevrimdışıyken kuyrukta bekletme, sunucuda oran sınırı.

## Out of scope

- Kullanıcı yolculuğu ekranı; oturum videosu kaydı (yapılmayacak).

## Dependencies

- V1-RMD-533

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; gerçek Postgres testi; çıktılar `evidence/V1-RMD-541/` altındadır.

## Handoff

- None

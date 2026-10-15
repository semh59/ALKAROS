# V1-RMD-552 - Çöküş kanalı ve süreç dışı bekçi

- Task ID: V1-RMD-552
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Veritabanı veya API süreci çökse de kritik durum yerel dosya tamponuna yazılır, doğrudan bildirim kanalına gider ve süreç dışı bir bekçi "sistem ayakta" nabzını izler.

## Owned surface

- `plan/v1/remediation/V1-RMD-552-outage-channel-and-watchdog.md`
- `evidence/V1-RMD-552/**`
- `src/Host/Observability/CrashChannel/**`
- `tests/Host/Observability/CrashChannel/**`
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Boyutu sınırlı yerel dosya tamponu; veritabanı yazımı başarısızsa kritik olay kuyruğu atlayıp doğrudan gönderim denemesi; devre kesici.
- Süreç dışı bekçi (connector konteyneri): API sağlık ucunu izler, kapanırsa bildirim gönderir; dakikada bir nabız.

## Out of scope

- Gerçek Telegram doğrulaması (V1-RMD-557).

## Dependencies

- V1-RMD-533

## Acceptance evidence

1. Veritabanı durdurulunca 60 sn içinde bildirim kuyruğuna veya doğrudan kanala kritik olay düşer.
2. API konteyneri durdurulunca bekçi 90 sn içinde alarm üretir.
3. Tampon üst sınırı 5 MB; dolunca en eski kayıt atılır ve sayılır.
4. Çıktılar `evidence/V1-RMD-552/` altındadır.

## Handoff

- None

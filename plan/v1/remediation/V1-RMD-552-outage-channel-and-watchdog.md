# V1-RMD-552 - Çöküş kanalı ve süreç dışı bekçi

- Task ID: V1-RMD-552
- Status: Done
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
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Boyutu sınırlı yerel dosya tamponu; veritabanı yazımı başarısızsa kritik olay kuyruğu atlayıp doğrudan gönderim denemesi; devre kesici.
- Bekçi mantığı (sağlık ucu yoklama, ardışık başarısızlıkta tek alarm, nabız sayacı) ve doğrudan bildirim arayüzü; bekçinin ayrı konteynerde çalıştırılması ve gerçek kanal V1-RMD-585'te bağlanır.

## Out of scope

- Gerçek Telegram doğrulaması (V1-RMD-557) ve bekçi konteyneri (V1-RMD-585).

## Dependencies

- V1-RMD-533

## Acceptance evidence

1. Bildirim kanalı kapalıyken kritik olay yerel tampona düşer, devre kesici açılır; kanal dönünce tampon boşaltılır.
2. Sağlık ucu ardışık 2 yoklamada başarısızsa bekçi tek alarm üretir; sağlıklı yoklama alarmı sıfırlar.
3. Tampon üst sınırı 5 MB; dolunca en eski kayıt atılır ve sayılır.
4. Çıktılar `evidence/V1-RMD-552/` altındadır.

## Handoff

- None

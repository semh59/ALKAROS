# V1-RMD-585 - Süreç dışı bekçi konteyneri ve Telegram doğrudan bildirimi

- Task ID: V1-RMD-585
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: feature
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

API veya veritabanı tamamen çöktüğünde hata kayıt sistemi de çalışmaz; bu yüzden sistemin ayakta olduğunu ayrı bir konteynerden yoklayan ve çökmede Telegram'a doğrudan haber veren bekçi gerçekten çalıştırılır. Bekçi mantığı ve dosya tamponu V1-RMD-552'de yazıldı ama onu çalıştıran süreç ve gerçek gönderici hiçbir görevde bağlanmadı.

## Owned surface

- `plan/v1/remediation/V1-RMD-585-outage-watchdog-container.md`
- `evidence/V1-RMD-585/**`
- `src/Host/Observability/OutageWatchdog/**`
- `tests/Host/Observability/OutageWatchdog/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Program.cs - yalnız `watchdog` alt komutu
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-552-outage-channel-and-watchdog.md - yalnız bekçi konteynerinin sahibini gösteren iki satır
- Sınırlı ek (paylaşılan, geri-tik olmadan): compose.yaml - yalnız yeni `watchdog` servisi ve tampon birimi
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Telegram doğrudan göndericisi (sabit künye metni, serbest metin yok); bot anahtarı yalnız çalışma anında ortam değişkeninden okunur ve hiçbir dosyaya veya log satırına yazılmaz.
- `watchdog` alt komutu: sağlık ucunu belirli aralıkla yoklar, çökmede tek alarm verir, kanal kapalıysa yerel dosya tamponuna yazar, düzelince boşaltır.
- Bekçinin API'den ayrı konteyner olarak çalışması ve gerçek yığında API durdurulunca alarmın tampona düştüğünün kanıtı.

## Out of scope

- Gerçek Telegram hesabıyla teslim doğrulaması (V1-RMD-557).

## Dependencies

- V1-RMD-552
- V1-RMD-557

## Acceptance evidence

1. Gönderici, bekçi döngüsü ve alt komut için testler; her biri için mutasyon kanıtı.
2. Gerçek Docker yığınında API durdurulunca bekçinin tek alarm verdiği ve tamponda `system.unreachable` satırı bulunduğu, API dönünce tamponun boşaldığı çıktı.
3. Bot anahtarı hiçbir dosyada yoktur (tarama çıktısı).
4. Çıktılar `evidence/V1-RMD-585/` altındadır.

## Handoff

- None

# V1-RMD-586 - Sistem sağlığı ekranlarında eski ölçümün işaretlenmesi

- Task ID: V1-RMD-586
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: feature
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Sağlık ölçümü periyodik görevle yazılır; görev durur veya başarısız olursa iki ekran (mutfak yönetimi ve sistem sağlığı) en son ölçümü zamanını göstermeden "Sağlıklı" diye göstermeye devam eder. Ölçüm 3 dakikadan eskiyse ekran bunu açıkça belirtir ve son ölçüm zamanını gösterir.

## Owned surface

- `plan/v1/remediation/V1-RMD-586-health-snapshot-staleness.md`
- `evidence/V1-RMD-586/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/kitchen-operations/** - yalnız eski ölçüm yardımcısı ve sağlık paneli
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/system-health/** - yalnız eski ölçüm uyarısı ve testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts - yalnız yeni metin
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-555-health-snapshot-wiring.md - yalnız Out of scope satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Ölçüm yaşı eşiği (3 dakika, ölçüm aralığının üç katı), eski ölçüm uyarı metni ve iki ekranda gösterimi; geçerli tarih okunamayan ölçüm de eski sayılır.

## Out of scope

- Ölçümü yazan görevin davranışı; veritabanı tamamen çöktüğünde ekranlar zaten açılamaz, o durumu V1-RMD-585 bekçisi bildirir.

## Dependencies

- V1-RMD-555
- V1-RMD-585

## Acceptance evidence

1. Eşik sınırında (tam 3 dakika ve bir saniye fazlası), geçersiz tarihte ve taze ölçümde testler; mutasyon kanıtı.
2. Tür denetimi, lint ve ilgili test dosyaları sıfır hata.
3. Çıktılar `evidence/V1-RMD-586/` altındadır.

## Handoff

- None

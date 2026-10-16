# V1-RMD-541 - Arayüz etkileşim izleme

- Task ID: V1-RMD-541
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Kasa, garson ve mutfak ekranlarında basılan her düğme, sayfa değişimi ve form gönderimi etkileşim olarak kaydedilir: ne, ne zaman, hangi rol/terminal, ne kadar sürdü, sonucu ne oldu.

## Owned surface

- `plan/v1/remediation/V1-RMD-541-ui-interaction-telemetry.md`
- `evidence/V1-RMD-541/**`
- `src/Clients/PosTerminal/src/features/interaction-telemetry/**`
- `src/Host/Observability/InteractionTelemetry/**`
- `tests/Host/Observability/InteractionTelemetry/**`
- `database/migrations/V1/V1-RMD-541/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız uç nokta ve servis kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/main.tsx - yalnız etkileşim kaydının kurulumu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız uç nokta ve servis kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/main.tsx - yalnız etkileşim kaydının kurulumu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Observability/ServerDetectors/ServerDetectors.cs - yalnız saklama kataloğuna ham tablo satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): deploy/docker/backup.sh - yalnız loglanmayan tabloların verisini yedekten çıkaran seçenek
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `data-telemetry-id` zorunludur (yoksa eşleme tablosundan sabit anahtar); görünen metin, erişilebilir ad veya girilen değer asla kaydedilmez; anahtar sayısı sınırlıdır.
- Süre: dokunuştan ekranın güncellenmesine (INP benzeri), uzun görev (50 ms üstü) ve düzen kayması; toplu gönderim; çevrimdışıyken sınırlı kuyruk; ayrı depolama kotası (sipariş kuyruğunu etkilemez).
- Terminal başına ayrı yazma kotası (sipariş yazımlarını yemez).

## Out of scope

- Oturum videosu kaydı yapılmaz; vanilla uygulamalar V1-RMD-553.
- Her düğmeye `data-telemetry-id` eklenmesi V1-RMD-567'ye, gerçek kasa cihazında tıklama ek yükü ölçümü V1-RMD-556'ya taşındı.

## Dependencies

- V1-RMD-533
- V1-RMD-549
- V1-RMD-542

## Acceptance evidence

1. Anahtar sırası: `data-telemetry-id`, yoksa ad tablosundan sabit anahtar, yoksa `auto:` anahtarı; görünen metin ve girilen değer hiçbir anahtara girmez. Statik tarama PosTerminal düğme sitelerini sayar ve anahtarsız olanları `evidence/V1-RMD-541/button-keys.txt` olarak raporlar.
2. Telemetri kuyruğu dolunca sipariş kuyruğu yazımı etkilenmez (ayrı depolama anahtarı ve sınır testi).
3. Ham tıklama tablosu `raw_` önekli ve kalıcı günlük tutmayan (UNLOGGED) olur; saatlik yedek dosyasında yer almadığı gerçek `pg_dump` ile testle kanıtlanır; saklama kataloğuna 30 gün ile eklenir.
4. Çıktılar `evidence/V1-RMD-541/` altındadır.

## Handoff

- None

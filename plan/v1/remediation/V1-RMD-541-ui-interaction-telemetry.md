# V1-RMD-541 - Arayüz etkileşim izleme

- Task ID: V1-RMD-541
- Status: InProgress
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
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `data-telemetry-id` zorunludur (yoksa eşleme tablosundan sabit anahtar); görünen metin, erişilebilir ad veya girilen değer asla kaydedilmez; anahtar sayısı sınırlıdır.
- Süre: dokunuştan ekranın güncellenmesine (INP benzeri), uzun görev (50 ms üstü) ve düzen kayması; toplu gönderim; çevrimdışıyken sınırlı kuyruk; ayrı depolama kotası (sipariş kuyruğunu etkilemez).
- Terminal başına ayrı yazma kotası (sipariş yazımlarını yemez).

## Out of scope

- Oturum videosu kaydı yapılmaz; vanilla uygulamalar V1-RMD-553.

## Dependencies

- V1-RMD-533
- V1-RMD-549
- V1-RMD-542

## Acceptance evidence

1. PosTerminal'deki tüm düğmeler eşleme tablosunda anahtara sahiptir (statik tarama testi).
2. Tıklama ek yükü gerçek kasa cihazında p95 5 ms altında.
3. Telemetri kuyruğu dolunca sipariş kuyruğu yazımı etkilenmez.
4. Ham tıklama tabloları `raw_` önekli ve kalıcı günlük tutmayan (UNLOGGED) olur; saatlik yedek dosyasında yer almadığı testle kanıtlanır; saklama kataloğuna 30 gün ile eklenir.
5. Çıktılar `evidence/V1-RMD-541/` altındadır.

## Handoff

- None

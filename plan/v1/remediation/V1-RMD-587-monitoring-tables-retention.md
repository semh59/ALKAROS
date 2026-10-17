# V1-RMD-587 - Sağlık ve bildirim tablolarında saklama süresi

- Task ID: V1-RMD-587
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: feature
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Günlük silme işi yalnız hata olayı, hata kaydı ve ham tıklama tablolarını temizliyor. Dakikada bir yazılan sağlık ölçümü (`operations.system_health_snapshots`) ve sağlık denetimi (`observability.health_checks`) ile bildirim kuyruğu ve bastırma tabloları hiç silinmiyor; yılda yarım milyonu aşan satır birikir. Bu tablolar da sürelerine göre silinir.

## Owned surface

- `plan/v1/remediation/V1-RMD-587-monitoring-tables-retention.md`
- `evidence/V1-RMD-587/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Observability/ServerDetectors/** - yalnız silme işinin tablo listesi ve sağlık denetimi silmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Observability/ServerDetectors/** - yalnız yeni testler ve test veritabanı göçleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Observability/Foundation/HealthCheckEnums.cs - yalnız saklama kataloğuna gün karşılıkları
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-549-telemetry-privacy-retention.md - yalnız Out of scope satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Sağlık ölçümü, bildirim kuyruğu ve bildirim bastırma tabloları 30 gün; sağlık denetimi satırları kendi onaylı saklama kimliğinin gün sayısı kadar (7, 30, 90, 365) saklanır.

## Out of scope

- Uyarı (`alerts`) ve uyarı olayı (`alert_events`) tabloları: eklenir-silinmez denetim izi olarak tasarlandı, süresi PO kararı gerektirir.

## Dependencies

- V1-RMD-549

## Acceptance evidence

1. Gerçek Postgres testi: her tabloda eski satır silinir, yeni satır kalır; sağlık denetimi satırları kendi saklama kimliğine göre silinir; denetim kaydında tablo adları görünür.
2. Mutasyon kanıtı.
3. Çıktılar `evidence/V1-RMD-587/` altındadır.

## Handoff

- None

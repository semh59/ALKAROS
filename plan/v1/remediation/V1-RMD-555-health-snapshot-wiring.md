# V1-RMD-555 - Sistem sağlığı görüntüsünü yazma

- Task ID: V1-RMD-555
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Mevcut sistem sağlığı görüntüsünü yazan kod bağlanır; yönetim ekranındaki boş "sağlık" bölümü gerçek veriyle dolar.

## Owned surface

- `plan/v1/remediation/V1-RMD-555-health-snapshot-wiring.md`
- `evidence/V1-RMD-555/**`
- `src/Host/Observability/HealthSnapshot/**`
- `tests/Host/Observability/HealthSnapshot/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız periyodik görev kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Periyodik görev `CaptureSystemHealthSnapshotAsync` çağırır; disk, yedek ve veritabanı kontrolleri doldurulur.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-533

## Acceptance evidence

1. Görüntü 60 sn aralıkla yazılır; yedek yaşı ve disk doluluğu ekranda sıfırdan farklı gerçek değer gösterir.
2. Çıktılar `evidence/V1-RMD-555/` altındadır.

## Handoff

- None

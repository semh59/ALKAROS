# V1-RMD-554 - Güvenlik, iş kuralı ve entegrasyon dedektörleri

- Task ID: V1-RMD-554
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

Yetki reddi yağmuru, kaba kuvvet giriş denemesi ve outbox kalıcı başarısız mesajlar hata olayı olur.

## Owned surface

- `plan/v1/remediation/V1-RMD-554-security-and-rule-detectors.md`
- `evidence/V1-RMD-554/**`
- `src/Host/Observability/SecurityDetectors/**`
- `tests/Host/Observability/SecurityDetectors/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız dedektör kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Endpoints.cs - yalnız giriş başarısızlığı bildirimi
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Mevcut yetki reddi olay kaynağı (denial event sink) dedektörü; outbox kalıcı başarısızlığı artık yalnız günlükte kalmaz. QR tüneli sağlık sinyali V1-RMD-558 kapsamına taşındı (bağlayıcı ayrı süreç).
- Giriş denemesi eşiği: 60 sn'de 10 başarısız; olayda kullanıcı adı yerine terminal etiketi.

## Out of scope

- Yok.

## Dependencies

- V1-RMD-533

## Acceptance evidence

1. 60 sn'de 10 başarısız giriş 1 olay üretir.
2. Outbox'ta kalıcı başarısız mesaj 1 dakika içinde olay olur.
3. Çıktılar `evidence/V1-RMD-554/` altındadır.

## Handoff

- None

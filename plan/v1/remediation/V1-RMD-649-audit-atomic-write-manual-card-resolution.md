# V1-RMD-649 - Denetim kaydının iş işlemiyle birlikte yazılması (manuel kart çözümleme)

- Task ID: V1-RMD-649
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Denetim kaydı 10 yıl saklanır (Ürün Sahibi kararı 2026-10-20) ve iz kaybı kabul edilmez. Manuel kart çözümleme akışlarında (kart çekilmedi, kart çekildi talebi, onay, ret) para kaydı ve ödeme durumu değişiyor ama denetim kaydı işlemden sonra ayrı bağlantıda yazılıyordu. Ödeme modülü, çağıranın verdiği bir kaydedici ile olayı kendi işleminin içinde, commit'ten önce yazar (modül sınırı korunur: Ödeme modülü Audit modülüne bağımlı olmaz). Denetim yazılamazsa çözümleme de kalıcı olmaz. Kaynak: V1-RMD-595 bulgusu F01 (çift ekran ödeme çözümleme çağrı noktaları).

## Owned surface

- `plan/v1/remediation/V1-RMD-649-audit-atomic-write-manual-card-resolution.md`
- `evidence/V1-RMD-649/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-648-audit-atomic-write-order-exceptions.md - yalnız Handoff satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/ManualResolution/IManualPaymentResolutionService.cs - yalnız kaydedici temsilcisi ve imza
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/ManualResolution/ManualPaymentResolutionService.cs - yalnız işlem içi kayıt
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/ManualResolution/ManualCardConfirmationService.cs - yalnız işlem içi kayıt
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Payments.cs - yalnız denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs - yalnız denetim atomikliği testleri
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `MarkNotChargedAsync`, `RequestAsync`, `ApproveAsync`, `RejectAsync` işlem içinde çalışan zorunlu bir kaydedici alır; uç noktalar olayları bu kaydedici ile yazar, işlem sonrası çağrılar kalkar.

## Out of scope

- Gönderilmiş kalem iptali ve diğer modüllerin denetim yazımları (ayrı görevler).

## Dependencies

- V1-RMD-648

## Acceptance evidence

1. Önce kırmızı (mutasyon) sonra yeşil gerçek PostgreSQL testi: denetim yazımı hata verirse çözümleme kalıcı olmaz; başarılı yolda tek denetim satırı.
2. Mutasyon kanıtı ve çıktılar `evidence/V1-RMD-649/` altındadır.

## Handoff

- V1-RMD-650

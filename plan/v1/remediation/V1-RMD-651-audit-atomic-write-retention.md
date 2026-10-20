# V1-RMD-651 - Denetim kaydının iş işlemiyle birlikte yazılması (saklama, imha ve yeniden şifreleme)

- Task ID: V1-RMD-651
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Denetim kaydı 10 yıl saklanır (Ürün Sahibi kararı 2026-10-20) ve iz kaybı kabul edilmez. Saklama süresi dolan kaydı imha etme, imha kuyruğundaki kaydı kalıcı silme ve yeniden şifreleme önce değişikliği yapıp sonra denetim yazıyordu; denetim yazılamazsa imha tamamlanmış ama izsiz kalıyordu ve sonraki koşu o kaydı artık işlemediği için iz hiç oluşmuyordu. Kayıt deposu, aynı işlemde bir kaydedici çalıştıran seçenekler kazanır; üç servis denetim olayını bu kaydedici ile yazar. Denetim yazılamazsa imha, silme ve yeniden şifreleme de kalıcı olmaz. Kaynak: V1-RMD-595 bulgusu F01 (saklama modülü çağrı noktaları).

## Owned surface

- `plan/v1/remediation/V1-RMD-651-audit-atomic-write-retention.md`
- `evidence/V1-RMD-651/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-650-audit-before-admin-actions.md - yalnız Handoff satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Security/DataProtectionRetention/IRetentionSubjectStore.cs - yalnız işlem içi kaydedici seçenekleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Security/DataProtectionRetention/PostgresRetentionSubjectStore.cs - yalnız işlem içi kaydedici seçenekleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Security/DataProtectionRetention/DeletionQueueProcessor.cs - yalnız denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Security/DataProtectionRetention/RetentionExecutionService.cs - yalnız denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Security/DataProtectionRetention/AuthorizedReEncryptionService.cs - yalnız denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Security/DataProtectionRetention/DeletionQueueProcessorTests.cs - yalnız denetim atomikliği testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Security/DataProtectionRetention/RetentionExecutionServiceTests.cs - yalnız denetim atomikliği testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Security/DataProtectionRetention/AuthorizedReEncryptionServiceTests.cs - yalnız denetim atomikliği testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Security/DataProtectionRetention/Fixtures/PurgeThrowingRetentionSubjectStore.cs - yalnız yeni seçenekler
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Security/DataProtectionRetention/Fixtures/RecordingAuditEventStore.cs - yalnız işlem içi yazım
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `MarkDisposedAsync`, `ReplaceEnvelopeAsync`, `PurgeAsync` için isteğe bağlı işlem içi kaydedici seçeneği; mevcut imzalar korunur.
- Üç servisin denetim olayını bu seçenekle yazması.

## Out of scope

- Müşteri anonimleştirme, performans bütçesi yönetimi, gözlemlenebilirlik dedektörleri, tanılama paketi ve şüpheli giriş kaydı (ayrı görevler veya olay-kaydı niteliğinde olanlar için gerekçeli kapanış).

## Dependencies

- V1-RMD-650

## Acceptance evidence

1. Önce kırmızı sonra yeşil gerçek PostgreSQL testi: denetim yazımı hata verirse imha, silme ve yeniden şifreleme kalıcı olmaz.
2. Mutasyon kanıtı ve çıktılar `evidence/V1-RMD-651/` altındadır.

## Handoff

- None

# V1-RMD-652 - Denetim kaydının iş işlemiyle birlikte yazılması (müşteri anonimleştirme)

- Task ID: V1-RMD-652
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Denetim kaydı 10 yıl saklanır (Ürün Sahibi kararı 2026-10-20) ve iz kaybı kabul edilmez. Müşteri anonimleştirme talebi oluşturma, engel kalkınca beklemeye alma ve anonimleştirmeyi tamamlama, durum değişikliğini yapıp sonra denetim yazıyordu; denetim yazılamazsa durum değişmiş ama izi kalmamış oluyordu. Talep deposu, durum değişikliğiyle aynı işlemde bir kaydedici çalıştıran seçenekler kazanır; servis üç denetim olayını bu seçeneklerle yazar. Anonimleştirme tamamlama yeniden denendiğinde profil zaten anonim olduğundan yalnız durum ve denetim yazılır. Kaynak: V1-RMD-595 bulgusu F01 (müşteri anonimleştirme çağrı noktaları).

## Owned surface

- `plan/v1/remediation/V1-RMD-652-audit-atomic-write-customer-anonymization.md`
- `evidence/V1-RMD-652/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-651-audit-atomic-write-retention.md - yalnız Handoff satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/CustomerData/AnonymizationState/ICustomerAnonymizationRequestStore.cs - yalnız işlem içi kaydedici seçenekleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/CustomerData/AnonymizationState/PostgresCustomerAnonymizationRequestStore.cs - yalnız işlem içi kaydedici seçenekleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/CustomerData/AnonymizationState/CustomerAnonymizationService.cs - yalnız denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/CustomerData/AnonymizationState/CustomerAnonymizationServiceTests.cs - yalnız denetim atomikliği testleri
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `CreateAsync` ve `UpdateStatusAsync` için işlem içi kaydedici seçeneği (mevcut imzalar korunur); servisin üç denetim olayını bu seçeneklerle yazması.

## Out of scope

- Performans bütçesi yönetimi, gönderilmiş kalem iptali, elle yazılmış kayıt satırları (ayrı görevler).

## Dependencies

- V1-RMD-651

## Acceptance evidence

1. Önce kırmızı (mutasyon) sonra yeşil gerçek PostgreSQL testi: denetim yazımı hata verirse talep oluşmaz, durum değişmez, anonimleştirme tamamlanmış sayılmaz; yeniden denemede tamamlanır.
2. Mutasyon kanıtı ve çıktılar `evidence/V1-RMD-652/` altındadır.

## Handoff

- None

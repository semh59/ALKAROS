# V1-RMD-655 - Ortak denetim yazıcısı ve Host'taki elle yazılmış kayıt satırları

- Task ID: V1-RMD-655
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Denetim kaydı 10 yıl saklanır (Ürün Sahibi kararı 2026-10-20). Altı yerde denetim satırı ham SQL ile yazılıyor ve ortak denetim mağazasının temizleyicisini ve doğrulamasını atlıyordu. Mağazanın ekleme gövdesi ortak, herkese açık bir denetim yazıcısına taşınır (mağaza onu kullanır); Host'taki iki ham yazım (bekleyen sipariş onayı, çevrimiçi sipariş notu görüntüleme) bu yazıcıya geçer. Modüllerdeki üç ham yazım için ayrı görev (modül bağımlılık kenarı gerekir). Kaynak: V1-RMD-595 bulgusu F09 (Host noktaları).

## Owned surface

- `plan/v1/remediation/V1-RMD-655-audit-shared-writer-host-sites.md`
- `evidence/V1-RMD-655/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-654-audit-atomic-write-sent-item-void.md - yalnız Handoff satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Audit/EventStore/PostgresAuditEventStore.cs - yalnız ekleme gövdesinin yazıcıya taşınması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Audit/EventStore/AuditEventWriter.cs - yeni ortak yazıcı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/PendingOrderConfirmation/PendingOrderConfirmationStore.cs - yalnız denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/OnlineOrdering/OnlineOperationsEndpoints.cs - yalnız denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Audit/EventStore/AuditTests.cs - yalnız yazıcı testleri
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `AuditEventWriter.WriteAsync(connection, transaction?, auditEvent, sanitizer?, ct)`; mağaza ona yönlenir; iki Host noktası ona geçer (aynı olay adları ve alanlar).

## Out of scope

- Orders ve OnlineOrdering modüllerindeki üç ham yazım (modül bağımlılık kenarı ve belge satırı gerektirir, ayrı görev).

## Dependencies

- V1-RMD-654

## Acceptance evidence

1. Yazıcı testi: kişisel/gizli alanlar temizlenir, geçersiz olay reddedilir ve hiçbir şey yazılmaz; iki Host noktasının mevcut testleri başarılı.
2. Mutasyon kanıtı ve çıktılar `evidence/V1-RMD-655/` altındadır.

## Handoff

- None

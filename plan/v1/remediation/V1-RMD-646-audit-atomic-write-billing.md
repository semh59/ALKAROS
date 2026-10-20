# V1-RMD-646 - Denetim kaydının iş işlemiyle birlikte yazılması (fatura iptali ve iade)

- Task ID: V1-RMD-646
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Denetim kaydı 10 yıl saklanır (Ürün Sahibi kararı 2026-10-20) ve iz kaybı kabul edilmez. `IAuditEventStore` çağıranın bağlantısı ve işlemi üzerinden yazan bir aşırı yükleme kazanır (aynı doğrulama ve temizleme yolu); fatura iptali ve iade akışları (talep reddi, vardiya iade limiti, iade tamamlama, nakit ve manuel iade) denetim kaydını iş işleminin commit'inden önce, aynı işlemde yazar. Denetim kaydı yazılamazsa iş de kalıcı olmaz. Kaynak: V1-RMD-595 bulguları F01 (fatura iptali ve iade kısmı), F18, F20 (ham komut yinelemesi ve boş öğe).

## Owned surface

- `plan/v1/remediation/V1-RMD-646-audit-atomic-write-billing.md`
- `evidence/V1-RMD-646/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-645-audit-append-only-and-disposal-hardening.md - yalnız Handoff satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Audit/EventStore/IAuditEventStore.cs - yalnız işlem içi aşırı yükleme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Audit/EventStore/PostgresAuditEventStore.cs - yalnız işlem içi aşırı yükleme ve ortak insert
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/BillCancellation.cs - yalnız denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/PaymentRefunds.cs - yalnız denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Audit/EventStore/AuditTests.cs - yalnız işlem içi aşırı yükleme testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/BillCancellationHttpTests.cs - yalnız denetim atomikliği testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/PaymentRefundHttpTests.cs - yalnız denetim atomikliği testi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `AppendAsync(AuditEvent, NpgsqlConnection, NpgsqlTransaction, CancellationToken)`; tek komut gövdesi tek INSERT'te paylaşılır, `AppendBatchAsync` boş öğeyi açık hata ile reddeder.
- BillCancellation ve PaymentRefunds içindeki denetim yazımları commit'ten önce işlemin içine alınır.

## Out of scope

- İndirim, bahşiş, sipariş kalemi iptal/ikram, çift ekran ödeme çözümleme ve diğer modüllerin denetim yazımları (V1-RMD-595 F01'in kalan çağrı noktaları ayrı görevlerle).
- Ham INSERT'lerin store'a taşınması (F09), DI ömrü düzenlemesi (F18 DI kısmı).

## Dependencies

- V1-RMD-645

## Acceptance evidence

1. Her kabul koşulu için önce kırmızı sonra yeşil gerçek PostgreSQL testi: denetim yazımı hata verirse iş geri alınır (iptal ve iade).
2. Mutasyon kanıtı ve çıktılar `evidence/V1-RMD-646/` altındadır.

## Handoff

- None

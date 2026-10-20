# V1-RMD-647 - Denetim kaydının iş işlemiyle birlikte yazılması (indirim ve bahşiş)

- Task ID: V1-RMD-647
- Status: InProgress
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Denetim kaydı 10 yıl saklanır (Ürün Sahibi kararı 2026-10-20) ve iz kaybı kabul edilmez. Hesap indirimi ve bahşiş, kayıt satırıyla aynı işlemde denetim kaydı yazar: denetim yazılamazsa indirim veya bahşiş de kalıcı olmaz, aynı anahtarla tekrar gelen istek ikinci kez denetim satırı üretmez. Bahşişin şimdiye kadar hiç denetim kaydı yoktu; `bill.tip.applied` eklenir. Kaynak: V1-RMD-595 bulgusu F01 (indirim ve bahşiş çağrı noktaları).

## Owned surface

- `plan/v1/remediation/V1-RMD-647-audit-atomic-write-discount-tip.md`
- `evidence/V1-RMD-647/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-646-audit-atomic-write-billing.md - yalnız Handoff satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/BillingSplitStore.cs - yalnız indirim ve bahşiş denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/BillingSplitApplication.cs - yalnız indirim ve bahşiş denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Billing/BillingSplitHttpTests.cs - yalnız denetim atomikliği testleri
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- İndirim ve bahşiş mağaza metotları denetim mağazasını ve ilişki kimliğini alır; olayı kayıt satırıyla aynı işlemde yazar. Tekrar (replay) yolu denetim satırı eklemez. Uç noktadaki işlem sonrası denetim çağrısı kalkar.

## Out of scope

- Sipariş kalemi iptal/ikram, çift ekran ödeme çözümleme ve diğer modüllerin denetim yazımları (ayrı görevler).

## Dependencies

- V1-RMD-646

## Acceptance evidence

1. Önce kırmızı sonra yeşil gerçek PostgreSQL testi: denetim yazımı hata verirse indirim ve bahşiş kalıcı olmaz; başarılı yolda tek denetim satırı; aynı anahtarlı tekrar ikinci satır üretmez.
2. Mutasyon kanıtı ve çıktılar `evidence/V1-RMD-647/` altındadır.

## Handoff

- None

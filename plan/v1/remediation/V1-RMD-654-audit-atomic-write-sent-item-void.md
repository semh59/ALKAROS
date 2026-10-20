# V1-RMD-654 - Gönderilmiş kalem iptalinde denetim kaydının sipariş işleminde yazılması

- Task ID: V1-RMD-654
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Denetim kaydı 10 yıl saklanır (Ürün Sahibi kararı 2026-10-20) ve iz kaybı kabul edilmez. Gönderilmiş kalem iptalinde para taşıyan karar (sipariş kaydı ve stok iadesi) tek işlemde kalıcılaşıyor, ardından mutfak ve hesap adımları ayrı işlemlerle yapılıyor ve denetim kaydı en sonda ham SQL ile, temizleyici ve doğrulama atlanarak yazılıyordu. Denetim kaydı (`Order.SentItemVoided`) artık sipariş işleminin içinde ortak denetim mağazasıyla yazılır (temizleyici ve doğrulama dahil); yazılamazsa iptal de kalıcı olmaz. Mutfak ve hesap adımlarının sonucu zaten uç noktanın `order-item.void-sent` olayında kayıtlıdır. Kaynak: V1-RMD-595 bulguları F01 ve F09 (gönderilmiş kalem iptali).

## Owned surface

- `plan/v1/remediation/V1-RMD-654-audit-atomic-write-sent-item-void.md`
- `evidence/V1-RMD-654/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-653-audit-before-performance-budget-change.md - yalnız Handoff satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/SentItemVoid/SentItemVoidStore.cs - yalnız denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/VoidSent/OrderManagementVoidSentHttpTests.cs - yalnız denetim atomikliği testi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- `SentItemVoidStore` denetim olayını ortak denetim mağazasıyla sipariş işleminin içinde yazar; ham INSERT kalkar.

## Out of scope

- Mutfak ve hesap adımlarının tek işleme alınması (V1-IAM-027'de kabul edilmiş ödünleşim), diğer ham INSERT noktaları (ayrı görev).

## Dependencies

- V1-RMD-653

## Acceptance evidence

1. Önce kırmızı (mutasyon) sonra yeşil gerçek PostgreSQL testi: `Order.SentItemVoided` yazılamazsa kalem iptal edilmez, stok iade edilmez.
2. Mutasyon kanıtı ve çıktılar `evidence/V1-RMD-654/` altındadır.

## Handoff

- None

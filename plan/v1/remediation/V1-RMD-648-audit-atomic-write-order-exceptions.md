# V1-RMD-648 - Denetim kaydının iş işlemiyle birlikte yazılması (kalem iptali, ikram, hesabı geri alma)

- Task ID: V1-RMD-648
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Denetim kaydı 10 yıl saklanır (Ürün Sahibi kararı 2026-10-20) ve iz kaybı kabul edilmez. Kalem iptali ve ikram zaten iş işleminde denetim satırı yazıyor (`Order.ItemVoided`, `Order.ItemComplimentary`); uç noktadaki ikinci, işlem sonrası kayıt (`order-item.voided`, `order-item.complimentary-applied`) aynı olayı ikinci kez ve atomik olmadan yazıyor, kayıt başarısız olursa istemciye iş yapılmışken hata dönüyor. Bu ikinci kayıtlar kaldırılır, kayıt iddiaları atomik olaya yönlendirilir. Hesabı kasadan geri alma (`check.recalled-from-cashier`) denetim satırını geri alma işleminin içinde yazar; yazılamazsa geri alma da kalıcı olmaz. Kaynak: V1-RMD-595 bulgusu F01 (Y1 dahil, kalem iptali/ikram ve geri alma çağrı noktaları).

## Owned surface

- `plan/v1/remediation/V1-RMD-648-audit-atomic-write-order-exceptions.md`
- `evidence/V1-RMD-648/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-647-audit-atomic-write-discount-tip.md - yalnız Handoff satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/OrderManagementEndpoints.cs - yalnız denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/CashierHandoffStore.cs - yalnız geri alma denetim yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/Void/OrderManagementVoidHttpTests.cs - yalnız denetim iddiası
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/Comp/OrderManagementCompHttpTests.cs - yalnız denetim iddiası
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/TableDraft/CheckLifecycleHttpTests.cs - yalnız geri alma denetim testleri
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Uç noktalardaki `order-item.voided` ve `order-item.complimentary-applied` kayıtlarının kaldırılması; testlerin atomik olayı doğrulaması.
- `RecallCheckAsync` denetim mağazası, aktör ve ilişki kimliği alır; "Recalled" yolunda olayı aynı işlemde yazar.

## Out of scope

- Gönderilmiş kalem iptali (`order-item.void-sent`): akış tek işlemde değil, ayrı görevle ele alınır.
- Çift ekran ödeme çözümleme ve diğer modüllerin yazımları (ayrı görevler).

## Dependencies

- V1-RMD-647

## Acceptance evidence

1. Kalem iptali ve ikramda tek denetim satırı (atomik olay); geri almada denetim hata verirse geri alma kalıcı olmaz (önce kırmızı, sonra yeşil gerçek PostgreSQL testi).
2. Mutasyon kanıtı ve çıktılar `evidence/V1-RMD-648/` altındadır.

## Handoff

- V1-RMD-649

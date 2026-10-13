# V1-RMD-497 - Faturalama güvenliği (faturalanmış kaynak seti iptali, online taslak kuyruğu açlığı)

- Task ID: V1-RMD-497
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Bağımsız taramanın bulduğu iki faturalama riskini kapatmak: faturası üretilmiş bir kaynak setinin iptal edilip aynı borç kalemlerinin yeni sete girerek çift faturalanabilmesi, ve online sipariş fatura taslağı geçişinde kalıcı başarısız siparişlerin 50'lik kuyruğu doldurup yeni siparişlerin taslağını engellemesi.

## Owned surface

- `plan/v1/remediation/V1-RMD-497-invoicing-safety.md`
- `evidence/V1-RMD-497/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Invoicing/SourceSelection/PostgresInvoiceSourceSelectionService.cs ve src/Modules/Invoicing/SourceSelection/InvoiceSourceSelectionExceptions.cs — yalnız faturalanmış setin iptalinin reddi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Invoicing/SourceSelection/InvoiceSourceSelectionTests.cs ve tests/Modules/Invoicing/SourceSelection/Fixtures/InvoiceSourceSelectionTestDatabase.cs — yalnız bu görevin testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/OnlineOrdering/OnlineOrderInvoiceDrafting.cs ve tests/Host/Experience/OnlineOrdering/OnlineOperationsHttpTests.cs — yalnız taslak geçişi sıralaması
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Faturası üretilmiş bir kaynak setinin iptali reddedilir (set ve satırları olduğu gibi kalır).
- Online sipariş taslak geçişi her turda en eski kalıcı başarısızlarla dolmaz: aday siparişler rastgele sırayla alınır, böylece 50'den fazla kalıcı başarısız sipariş olsa da diğerleri taslak alır.

## Out of scope

- Fatura iptal akışı ve gönderim (`V14-INV-004`, `V14-QNB-005`).

## Dependencies

- V14-INV-003

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-497/` altındadır.

## Handoff

- None

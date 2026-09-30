# V1-RMD-472 - Teslimde online sipariş fatura taslağını açmak

- Task ID: V1-RMD-472
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-30

## Goal

Online sipariş teslim edildiğinde (`HandOverAsync`) sipariş ve kalemleri okunup `V1-RMD-470` taslağı açılır. Fatura başarısızlığı teslimi
engellemez: taslak teslimden sonra açılır, başarısız ya da satıcı bilgisi eksik ise sipariş "faturasız" kalır ve zamanlanmış bir tarama
(`V1-RMD-463` kalıbı) eksik taslakları tamamlar; 7 günlük yasal süre yaklaşan faturasız sipariş uzlaştırma vakası olur (Türkçe
etiket). Tekrar çağrı idempotenttir. Yemeksepeti ve Trendyol Go aynı teslim yolunu kullanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-472-online-order-invoice-on-hand-over.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/OnlineOrdering/YemeksepetiStatusSyncService.cs - yalnız teslimden sonra taslak çağrısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/OnlineOrdering/OnlineOperationsEndpoints.cs - yalnız gerekirse bağımlılık kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Reconciliation/OnlineOrderReconciliationHostedService.cs - yalnız faturasız sipariş taraması
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/OnlineOrdering/YemeksepetiStatusSyncTests.cs - yalnız yeni davranışın testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/OnlineOrdering/TrendyolGoStatusSyncTests.cs - yalnız yeni davranışın testleri

## In scope

- Sipariş ve kalem okuma (Host tarafında), taslak çağrısı, başarısızlıkta tarama ile tamamlama, yaklaşan süre vakası.
- Testler: teslim sonrası taslak, tekrar çağrı, fatura hatasında teslim başarılı, tarama ile tamamlama, iki platform.

## Out of scope

- Fatura tutarı kuralları (`V1-RMD-470`); ekran (`V1-RMD-473`).

## Dependencies

- V1-RMD-470

## Acceptance evidence

- Testler ve gerçek Host denemesi (teslim edilen online sipariş için taslak oluşur); çıktılar `evidence/V1-RMD-472/` altındadır.

## Handoff

- None

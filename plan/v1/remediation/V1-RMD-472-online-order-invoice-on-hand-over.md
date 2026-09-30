# V1-RMD-472 - Teslimde online sipariş fatura taslağını açmak

- Task ID: V1-RMD-472
- Status: InProgress
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-30

## Goal

Online sipariş teslim edildiğinde (`hand-over` ucu; iki platform da aynı ucu kullanır) sipariş ve kalemleri okunup `V1-RMD-470` taslağı açılır. Fatura başarısızlığı teslimi
engellemez: taslak teslim işlemi tamamlandıktan sonra ayrı açılır; başarısız ya da satıcı bilgisi eksik ise sipariş "faturasız" kalır ve zamanlanmış tarama
(`V1-RMD-463` kalıbı) son 7 günün eksik taslaklarını tamamlar. Tekrar çağrı idempotenttir. Web adresi platformdan gelir; ödeme türü, ödeme tarihi ve taşıyıcı bilgisi
`V1-RMD-474` ile gelene kadar boştur. Faturasız kalan siparişin yasal süre uyarısı (uzlaştırma vakası) ayrı görevdir.

## Owned surface

- `plan/v1/remediation/V1-RMD-472-online-order-invoice-on-hand-over.md`
- `src/Host/Experience/OnlineOrdering/OnlineOrderInvoiceDrafting.cs`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/OnlineOrdering/OnlineOperationsEndpoints.cs - yalnız teslimden sonra taslak çağrısı ve kayıt
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Reconciliation/OnlineOrderReconciliationHostedService.cs - yalnız faturasız sipariş taraması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız taramanın bağımlılık kaydı gerekirse
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/OnlineOrdering/OnlineOperationsHttpTests.cs - yalnız yeni davranışın testleri ve fatura modülü kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/OnlineOrdering/ALKAROS.Host.Experience.OnlineOrdering.Tests.csproj - yalnız migration 171 fixture satırı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tools/consistency-audit/unreachable_services_allowlist.json - yalnız taslak servisinin kaydı Host çağrısıyla düşer

## In scope

- Sipariş ve kalem okuma (Host tarafında), taslak çağrısı, başarısızlıkta tarama ile tamamlama.
- Testler: teslim sonrası taslak (HTTP), tekrar çağrı, fatura hatasında teslim başarılı, satıcı bilgisi yokken teslim başarılı ve tarama ile tamamlama, iki platform.

## Out of scope

- Fatura tutarı kuralları (`V1-RMD-470`); ekran (`V1-RMD-473`); yasal süre uyarısı vakası (ayrı görev).

## Dependencies

- V1-RMD-470

## Acceptance evidence

- Testler ve gerçek Host denemesi (teslim edilen online sipariş için taslak oluşur); çıktılar `evidence/V1-RMD-472/` altındadır.

## Handoff

- None

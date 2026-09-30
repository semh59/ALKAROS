# V1-RMD-467 - KDV dahil satır fiyatı: sipariş ve adisyon çekirdeği

- Task ID: V1-RMD-467
- Status: InProgress
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-30

## Goal

Semih 2026-09-30'da menü fiyatının KDV dahil olduğunu onayladı (`docs/compliance/money-tax-business-date.md` de bunu söyler).
Kod ise KDV'yi fiyatın üstüne ekliyor: 100 TL ürün %10 KDV ile 110 TL tutuyor. Bu görev satır hesabını tek yerde düzeltir:
brüt = yuvarlanmış(adet x birim fiyat + seçenekler - indirim), KDV = brüt x oran / (100 + oran) (önce KDV, yarım yukarı),
net = brüt - KDV. Sipariş ve adisyon aynı yardımcıyı kullanır; ikram satırı ve adisyon düzeltmeleri (`BillAdjustment`) aynı
kuralla tutarlı olur. Veri tabanı kısıtları biçimden bağımsızdır, göç gerekmez; gerçek veri yoktur.

## Owned surface

- `plan/v1/remediation/V1-RMD-467-tax-inclusive-line-pricing-orders-billing.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/OrderAggregate/OrderMath.cs - yalnız KDV dahil brüt tutarı bölen ortak yardımcı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/OrderAggregate/OrderItem.cs - yalnız kurucu ve miktar değişikliği hesabı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Orders/ItemExceptions/ItemExceptionHandler.cs - yalnız ikram indirim tutarı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/BillFoundation/BillItem.cs - yalnız satır ara toplamı, KDV bölme ve ikram denetimi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/Adjustments/BillAdjustment.cs - yalnız net ve KDV bölmesini ortak yardımcıya taşıma
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Orders/OrderAggregate/OrderDomainTests.cs - yalnız KDV dahil beklentiler
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Orders/ItemExceptions/ItemExceptionsTests.cs - yalnız ikram beklentisi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/BillFoundation/BillDomainTests.cs - yalnız KDV dahil beklentiler
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/Adjustments/AdjustmentsDomainTests.cs - yalnız KDV dahil beklentiler
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/domain/complimentary-line-fiscal-representation.md - yalnız KDV dahil örnek tutarlar

## In scope

- `OrderMath` içinde tek bölme yardımcısı; `OrderItem` kurucusu ve `ChangeQuantity` brütten böler (100 TL, %10: KDV 9,09, net 90,91, brüt 100).
- `BillItem` ara toplamı brüt + indirim olur; ikram satırı `FromOrderItem` ile fırlatmadan kurulur; `BillAdjustment` aynı yardımcıyı kullanır.
- Kayıtlı (eski) satırlar yeniden hesaplanmaz.
- Tam çözüm testleri: Orders ve Billing test projeleri ile bunlara bağlı diğer modül testleri exit code 0.

## Out of scope

- Host, raporlama, çift ekran ve istemciler (`V1-RMD-468`, `V1-RMD-469`); fatura hesaplayıcı (zaten KDV dahil); veri göçü.

## Dependencies

- None

## Acceptance evidence

- Domain testleri, kırmızı mutasyon kanıtı (eski üste ekleme formülü testleri kırar) ve gerçek Host denemesi (100 TL, %10 ürün
  sipariş ve adisyonda 100 TL toplam, 9,09 KDV); çıktılar `evidence/V1-RMD-467/` altındadır.

## Handoff

- None

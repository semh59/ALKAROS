# V1-RMD-474 - Online siparişte indirim, finansman kaynağı ve ödeme bilgisini saklamak

- Task ID: V1-RMD-474
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-30

## Goal

Normalizasyon bugün indirimi, indirimi kimin karşıladığını (restoran ya da platform), teslimat ücretini ve ödeme türünü saklamıyor;
Yemeksepeti'nde indirim alanı hiç okunmuyor, Trendyol Go'da kupon yalnız bayrak (`totalSellerAmount`). Fatura için gerekli: restoranın
karşıladığı indirim (matrahı düşürür), platformun karşıladığı kısım (matrahta kalır, KDV Kanunu md. 20), ödeme türü (internet
satışı alanı). Bu görev alanları normalleştirmeye ve siparişe ekler.

ENGELLİ: alan adları doğrulanmamış (Yemeksepeti belgesiz, Trendyol Go `totalSellerAmount` doğrulanmadı) ve gerçek örnek yük yok.
Başlamadan önce yayımlanmış şema ya da örnek yük gerekir; bu olmadan görev başlatılmaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-474-online-order-discount-and-payment-capture.md`

## In scope

- Örnek yük ya da yayımlı şema bulunduğunda güncellenecek; kapsam o zaman yazılır.

## Out of scope

- Fatura üretimi (`V1-RMD-470`).
- Araştırma notu (2026-10-17, V1-RMD-584), kayıt durumunu ve kararı değiştirmez:
- Doğrulanmış alanlar (2026-10-16/17):
- Trendyol Go (developers.tgoapps.com `Event-Types-Payloads`): ürün tanesi düzeyinde `coupon{couponId,description,amount{seller,trendyol,total},sellerCoverageRatio}` ve `promotions[]{promotionId,amount{seller,trendyol,total},discountType,sellerCoverageRatio}`; sipariş düzeyinde `coupon.totalSellerAmount`; `payment.paymentType`, `payment.mealCard.cardSourceType`, `payment.onDelivery.paymentType`; `totalDeliveryPrice` yalnız restoranın kendi kuryesinde dolu. Bu kaydın "kupon yalnız bayrak" varsayımı **yanlıştır**; ancak örnek yük kendi içinde tutarsız (ürün kuponu seller 1,72, sipariş toplamı 0), bu yüzden gerçek yük şarttır.
- Yemeksepeti: Q-Commerce şemasında indirimi kimin karşıladığı yalnız ürün düzeyinde ve yalnız `VENDOR` olarak görünür. Delivery Hero POS şemasında `discounts[].sponsorships[].sponsor` PLATFORM|VENDOR|THIRD_PARTY (sipariş, ürün ve ek ürün düzeyinde), `price.deliveryFees[]`, `payment.status` (paid|pending), `payment.type` (serbest metin) ve `invoicingInformation` vardır (https://integration-middleware.stg.restaurant-partners.com/apidocs/pluginOrder.yaml). Hangi API'nin geçerli olduğu V0-YSP-001'deki teyide bağlıdır.
- Migros Yemek: resmi belge yoktur; üçüncü taraf GitHub'da sızmış olabilecek belgelerdeki alanlara dayanılmaz.
- Görev hâlâ Blocked: gerçek örnek yük ve Yemeksepeti sözleşme teyidi yok.

## Dependencies

- None

## Acceptance evidence

- Başlatıldığında tanımlanacak; çıktılar `evidence/V1-RMD-474/` altındadır.

## Handoff

- None

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

## Dependencies

- None

## Acceptance evidence

- Başlatıldığında tanımlanacak; çıktılar `evidence/V1-RMD-474/` altındadır.

## Handoff

- None

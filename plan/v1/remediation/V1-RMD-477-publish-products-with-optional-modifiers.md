# V1-RMD-477 - Yalnız isteğe bağlı seçeneği olan ürünleri platforma yayımlamak

- Task ID: V1-RMD-477
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-30

## Goal

Menü yayımlama, etkin herhangi bir seçenek grubu olan ürünü "zorunlu seçimleri platforma aktarılamıyor" diye atlıyor. Sipariş tarafı yalnızca zorunlu seçimi olan (`min_selections > 0`) ürünü reddediyor;
isteğe bağlı grubu olan ürün siparişte sorunsuz alınır. Yayımlama kuralı sipariş kuralıyla aynı olur: yalnız zorunlu seçim grubu olan ürün atlanır, isteğe bağlı gruplu ürün fiyat ve stok bilgisiyle yayımlanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-477-publish-products-with-optional-modifiers.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/OnlineOrdering/CatalogPublishing/CatalogPublicationService.cs - yalnız seçenek grubu sorgusu
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/OnlineOrdering/CatalogPublishing/CatalogPublicationTests.cs - yalnız yeni davranışın testleri

## In scope

- `has_modifiers` sorgusunu zorunlu gruba daraltmak; testler: yalnız isteğe bağlı gruplu ürün yayımlanır, zorunlu gruplu ürün `ModifiersNotSupported` ile atlanır.

## Out of scope

- Seçeneklerin platforma aktarılması (API desteklemiyor); Trendyol Go'da zorunlu seçimli ürün siparişi (API notları doğrulanmadı).

## Dependencies

- None

## Acceptance evidence

- Testler ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-477/` altındadır.

## Handoff

- None

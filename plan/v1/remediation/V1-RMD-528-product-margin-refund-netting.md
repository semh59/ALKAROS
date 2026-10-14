# V1-RMD-528 - Ürün marj raporunda kalem iadesinin netlenmesi

- Task ID: V1-RMD-528
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-14

## Goal

Ürün marj raporu, kalem bazlı iadede geri ödenen adedi ve cirosunu düşer; stoğa geri dönen adedin maliyetini de düşer. Bugün rapor ödenmiş hesabın satırlarını iadesiz toplar.

## Owned surface

- `plan/v1/remediation/V1-RMD-528-product-margin-refund-netting.md`
- `evidence/V1-RMD-528/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/ProductMargin/PostgresProductMarginReportService.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reporting/ProductMargin/ProductMarginReportTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reporting/ProductMargin/Fixtures/ProductMarginTestDatabase.cs
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Yalnız tamamlanmış (`Completed`) iade isteklerinin kalemleri düşer; bekleyen ve reddedilen düşmez.
- Satır adedi ve net tutarı iade edilen adet oranında azalır (satır net tutarı × iade adedi / satır adedi).
- Maliyet: aynı sipariş kaleminin stoğa dönen (`Return`, kaynak Order) hareketleri tüketimden düşer.

## Out of scope

- Kalem bilgisi olmayan (tüm ödeme) kısmi iade: kalemlere dağıtılamaz, rapor bunu netlemez.
- Hesap seviyesi indirimin kalemlere dağıtımı.

## Dependencies

- V1-RMD-523
- V1-RMD-524

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-528/` altındadır.

## Handoff

- None

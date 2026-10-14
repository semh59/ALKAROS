# V1-RMD-529 - Ürün marj raporunda kalemlere dağıtılmamış iade

- Task ID: V1-RMD-529
- Status: Planned
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-14

## Goal

Kalem seçilmeden tamamlanan iade ürün satırlarına yazılmaz; raporda ayrı toplam olarak görünür, böylece rapor kasadan çıkan paradan sapmaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-529-product-margin-unallocated-refund.md`
- `evidence/V1-RMD-529/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/ProductMargin/ProductMarginModels.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/ProductMargin/PostgresProductMarginReportService.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reporting/ProductMargin/ProductMarginReportTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reporting/ProductMargin/Fixtures/ProductMarginTestDatabase.cs
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Rapora `UnallocatedRefunds`: aralıktaki Ödendi hesapların, kalem satırı olmayan tamamlanmış iade isteklerinin tutar toplamı.
- Ürün satırları, `TotalNetRevenue` ve mevcut kontrol alanları değişmez.

## Out of scope

- Bu tutarın kalemlere dağıtılması; arayüzde gösterim.

## Dependencies

- V1-RMD-528

## Acceptance evidence

- Her kabul koşulu için test ve mutasyon kanıtı; çıktılar `evidence/V1-RMD-529/` altındadır.

## Handoff

- None

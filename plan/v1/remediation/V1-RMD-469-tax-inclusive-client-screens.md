# V1-RMD-469 - KDV dahil satır fiyatı: kasa, müşteri ekranı ve adisyon sayfası

- Task ID: V1-RMD-469
- Status: Done
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-30

## Goal

Kasa ekranı ürün fiyatını `fiyat x (1 + oran)` diye yeniden şişirip "KDV dahil" etiketiyle gösteriyor; fiyat zaten KDV
dahil olduğundan doğrudan birim fiyat gösterilir. "Ara toplam / KDV / Toplam" satırları toplanır gibi okunuyor; artık
toplam = ara toplam olduğundan satırlar "Toplam" ve "İçindeki KDV" olarak yeniden adlandırılır (müşteri ekranı, kasa,
müşteri adisyon sayfası).

## Owned surface

- `plan/v1/remediation/V1-RMD-469-tax-inclusive-client-screens.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/format.ts - yalnız KDV dahil birim fiyat yardımcısı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/Cashier.tsx - yalnız fiyat ve toplam satırları
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/CustomerDisplay.tsx - yalnız toplam satırları
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/CustomerDisplay.test.tsx - yalnız toplam beklentileri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/routes/Cashier.category-tabs.test.tsx - yalnız fiyat beklentileri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts - yalnız toplam satırı etiketleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Apps/CustomerWeb/Bill/wwwroot/bill.html - yalnız toplam satırı etiketleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Apps/CustomerWeb/Bill/wwwroot/bill.js - yalnız toplam satırı etiketleri

## In scope

- `grossUnitPrice` kaldırılır ya da birim fiyata indirgenir; toplam satır etiketleri Türkçe; ilgili istemci testleri.

## Out of scope

- Sunucu hesabı ve çift ekran sunucu tarafı (`V1-RMD-467`, `V1-RMD-468`).

## Dependencies

- V1-RMD-467

## Acceptance evidence

- Vitest, typecheck ve lint exit code 0; gerçek Host denemesi (kasa ekranında 100 TL ürün 100 TL görünür); çıktılar
  `evidence/V1-RMD-469/` altındadır.

## Handoff

- None

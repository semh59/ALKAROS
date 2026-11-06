# Eski Done görev kayıtlarının güncel karşılıkları

Done görev md ve kanıt dosyaları değiştirilmez; bu liste, kayıtların bugünkü kodla farkını verir. Kayıt kimlikleri doğrulama turundaki sıra numaralarıdır. Dosya:satır bilgileri doğrulama turunda koddan okunmuştur.

## Kanıt metinleri (evidence/*)

- F0051 evidence/V1-CUI-003/closure-evidence.txt: silinmiş OperationsStatus test projesini sayar.
- F0350 evidence/V1-BIL-004/verification.txt:12 ve F0373 evidence/V1-RMD-040/verification.md: anlatı ve çıkış kodu 0 beyanı var, koşu çıktısı yok.
- F0428 evidence/V1-RMD-603/README.md:21: derin okunmamış alanlar (güvenlik 74/120, performans ~46/120, backend 57/120) kabul edilmiş risk olarak yazılıdır.
- F0441 evidence/V13-CSH-004/closure-evidence.txt:26: `RequireCashierAsync` der; kodda `DualScreenApplication.CashSession.cs:47` `RequireCashDrawerAsync` kullanır.
- F0442 V13-GOV-005 ve F0443 V13-GOV-008: evidence klasörü yok; "113/113", "896/2001" sayıları kanıtsızdır.
- F0727 evidence/V1-REC-001/closure-evidence.txt:3 "Total 6 tests": CaseFoundation altında 8 + 1 = 9 test özniteliği vardır.
- F0894 evidence/V1-RMD-690/README.md:16: düzeltmeden sonra tüm Host.Tests yeniden koşulmamıştır.
- F0901..F0907 (V1-RMD-041/047/048/052/058/077): çıkış kodu 0 beyanı var, koşu çıktısı yok; V1-RMD-041 md `App.tsx` der (devir notu md:16); V1-RMD-077 evidence klasörü yok.

## Plan md metinleri

- F0398 V1-RMD-292:39-50: ödenecek tavan `split-payment.js:482-489` içinde düzeltilmiş tutardır.
- F0739 V1-RMD-421:23,56: iş günü toplamı `BusinessDayTotalsReader.cs:101-102` içinde Approved, Refunded ve PartiallyRefunded durumlarını okur.
- F0069 V1-RMD-328: `payment_reversals` tablosu vardır (`182-payment-reversals.up.sql:3`).
- F0070 V1-RMD-380: `AddOrderItemRequest` `Modifiers` taşır (`DualScreenContracts.cs:114-118`).
- F0352 V1-BIL-005: kod `SubjectServingUserId: order.ServingUserId` geçirir (`OrderManagementEndpoints.cs:705,837`).
- F0361 V1-CUI-011: dört senaryo ile başladı, `tests/E2E/Cashier/specs` 32 dosya (README güncellendi).
- F0372 V1-RMD-035: `MutateExistingItemAsync` kaynakta yoktur; mantık `order.ChangeItemQuantity` içindedir (`DualScreenStore.Orders.cs:299`).
- F0384 V1-RMD-212:55: yük sayı ise sıfır dahil `(N masa)` eklenir (`cashier-app.js:562`).
- F0387 V1-RMD-228:57,61: `BillItem.cs:169` brüt tutar artı indirim kullanır.
- F0393 V1-RMD-279: `PendingCheckSummaryV1` sonradan ItemPreview (280) ve TableId (281) aldı.
- F0394 V1-RMD-280:21: yenileme `PendingChecksWorkspace.tsx:12-14` içinde 60 sn güvenlik ağıdır; canlı itme V1-RMD-287.
- F0395 V1-RMD-286:27,43: açık çağrıları listeleyen uç `HelpRequestExperience.cs:139` içinde vardır.
- F0454 V14-ACC-003: kayıtlı politika `CreditTermsCreditPolicy` (`CustomerAccountsBillChargesModule.cs:30`).
- F0455 V14-ACC-005:19,75: nakit tahsilat `payments.payments` satırı yazmaz (`CashAccountReceiptHandler.cs:25`).
- F0870 V1-RMD-410:53-54 ve F0871 V1-RMD-413:58-61: sayılar testteki sayılardan farklıdır (`BillingSplitHttpTests.cs:539-595`: 250 hesap, 225 alınmış, 50 indirim reddedilir, 20 indirimle düzeltilmiş 230; 25 indirimde 225).
- F0899 V1-IAM-026: ikinci Amendment (2026-09-09) `void-complimentary-discount-policy.md:119-127` içindedir.
- F0918 V1-RMD-526:15: kanal raporu iadeyi V1-RMD-527 ile netler (`ChannelReportService.cs:73-77`).
- F0920 V1-WTR-020:39-42,65: Cashier split-payment bahşiş gönderir (`split-payment.js:68,724`).
- F0927 V13-PUI-004:87-91: EFT onay kutusu `split-payment.js:1875`, düğme pasifliği `:1666`, E2E `08-split-payment-eft.spec.js:37`.

## Başka görevde

- F0915 ok tuşu gezinmesi: V1-RMD-862.
- F0082 V1-RMD-738 (Billing csproj'da paket yok, repository Npgsql kullanıyor): açık soru olarak bırakıldı; bu görevde doğrulanmadı.

# V1-RMD-855 sonuç

Dört kasa yarışı gerçek HTTP + gerçek Postgres ile sınandı. Üretim kodu değişmedi; kusur çıkmadı.

| Yarış | Test | Dosya |
|---|---|---|
| iptal + tahsilat | `ACancelAndATenderOnTheSameBillNeverBothSucceed` (8 tur, iki dal da değişmezi doğrular), `ACancelATipOrATenderWaitsForTheLockTheOthersHoldAndRunsOnceItIsReleased` (iptal, tahsilat: `bill-settlement`; bahşiş, tahsilat: `payment-allocation`) | tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs |
| bahşiş + tahsilat | `ATipAndATenderThatPaysTheBillInFullNeverLeaveAPaidBillWithATipUnpaid` (8 tur), aynı kilit testi (bahşiş: `payment-allocation` kilidi) | aynı |
| iade çift onay / ret | `ParallelDecisionsOnOneRefundRequestApplyExactlyOnce` (iki onay + bir ret, tek 200, tek iade ve nakit satırı), `ARefundDecisionWaitsForTheBillLock...` (onay ve manuel tamamlama) | tests/Host/Experience/Billing/PaymentRefundHttpTests.cs |
| nakit hareketi + sayım/kapanış (F0431 R4) | `AMovementWaitsForACountThatIsStartingAndIsThenRefusedInsteadOfLandingBehindIt` (In, Out) | tests/Host/Experience/CashSession/CashMovementApprovalHttpTests.cs |

Tasarım kararı: gerçek zamanlama yarışı dar pencere nedeniyle mutantı öldürmedi (M1b, M2 ilk denemede yaşadı). Bu yüzden deterministik kilit testleri eklendi: test, kilidi kendi bağlantısında tutar, isteğin beklediğini kanıtlar, bırakır, sonucu doğrular. Soğuk ilk istek yanlış "bekliyor" sonucu vermesin diye aynı işlemden bir ısınma isteği yapılır.

## Mutasyon (cp+cmp ile geri alındı, mutations.log)

- M1a iptalde para kontrolü kaldırıldı: ÖLDÜ (tur testi).
- M1b iptalde `bill-settlement` kilidi kaldırıldı: ÖLDÜ (kilit testi; tur testi tek başına yaşıyordu).
- M2 bahşiş `payment-allocation` kilidi kaldırıldı: ÖLDÜ.
- M3 iade onayında kilit sonrası durum kontrolü kaldırıldı: ÖLDÜ.
- M7 iade isteği satırı `FOR UPDATE` kaldırıldı: ÖLDÜ.
- M4 onayda fatura kilidi kaldırıldı: çift onay testinde YAŞADI (aynı isteğe gelen kararları istek satırı kilidi `LoadForUpdate`, M7 öldürür, ve ödeme satırı `FOR UPDATE` sıraya sokar); `ARefundDecisionWaitsForTheBillLock...(false)` kilit testiyle ÖLDÜ.
- M5 manuel tamamlamada fatura kilidi kaldırıldı: `ARefundDecisionWaitsForTheBillLock...(true)` ile ÖLDÜ.
- M8 Eft tahsilatında `bill-settlement` kilidi kaldırıldı: ÖLDÜ (kilit testinde `tenders` durumu; kilit tutulurken ödeme satırı yazılmamış olmalı. Yalnız 'istek bekler' kontrolü yetmedi, çünkü kapanış servisi aynı kilidi sonradan alıyor).
- R4 `FOR SHARE` (PostgresCashTransactionLedgerRepository:95) kaldırıldı: ÖLDÜ, mevcut modül testi `StartingTheCountWaitsForAMovementAlreadyInFlightSoTheCountSeesIt` ile (V1-RMD-694 günlüğündeki "R4 YAŞADI" artık geçerli değil).
- R4b `FOR UPDATE` (satır 111) tek başına: yeni testlerle YAŞADI; R4 ile ikisi birlikte kaldırılınca ÖLDÜ (R4d). Host yolu önce `FOR UPDATE` alır (CashMovementStore.cs:44), sonra `FOR SHARE`. R4b mevcut `ConcurrentCashOutsCannotTogetherTakeMoreThanTheDrawerHolds` ve `AnApprovalAndACashOutRunningTogether...` testleriyle ÖLDÜ (mut-R4b-existing.log).

## Zaten kapalı (yeni test yazılmadı)

- Aynı kasa hareketine çift onay: `CashMovementApprovalHttpTests.ParallelDecisionsOnOneRequestApplyExactlyOnce`.
- Çift kapanış ve sayım+kapanış: `CashCloseSafetyHttpTests` (TwoParallelCloses..., ACountRecordedWhileTheSessionIsClosing...).

## Koşular (alkaros-test-pg, 55432)

Ön plan parçalı koşular, mutant derlemesinden sonra yeniden derlenmiş temiz derlemede: PaymentTender 17+19+23+23+25 = 107 başarılı (green-final-tender-*.log), CashSession kasa hareketi sınıfı 12+9+22+6 = 49 (green-final-cash-*.log), Billing iade sınıfı 46+29 = 75 (green-final-billing-*.log); hepsi 0 başarısız.

## Sınırlar

- Tur testleri (8 tur) iki sonuç dalını da geçerli sayar; tek başlarına mutantı öldürmez (M1b'yi öldüremedi). Yarışı asıl kilit testleri sınar; tur testleri kaba bozulmaya karşı sigortadır.
- Sayım başlangıcı host testinde SQL ile taklit edilir; gerçek sayım yolu `CashCloseSafetyHttpTests` ve modül testlerindedir.
- 1,5 sn bekleme yavaş ortamda kilitsiz de "bekliyor" gösterebilir; kilit mutantları öldüğü için kabul edildi.
- İade onayı ile iptal/tahsilat arasında zamanlamalı gerçek yarış yok; yerine her katılımcının (iptal, tahsilat, bahşiş, onay, manuel tamamlama) aynı kilidi aldığı kilit testleriyle sıralama kanıtlanır.
- Aynı ödemeye ikinci iade isteği: açık talep varken engelli (`OnlyOneOpenRequestPerPayment`), kısmi kalem iadelerinde birimleri talepler rezerve eder; paralel yarış yazılmadı.

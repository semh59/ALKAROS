# V1-RMD-879 - Hesap indiriminin kalemlere dağıtılması (F0287)

## Sonuç
Hesap indirimi artık tek yerde (`AdjustmentCalculator.DiscountedLineAmounts`) kalemlere brüt tutar oranında, kuruş hassasiyetinde dağıtılıyor (mevcut `BillMath.Apportion`, en büyük kalıntı). Kalem iadesi (`PaymentRefunds`) ve KDV kırılımı (`TaxBreakdown`) aynı dağıtımdan besleniyor.
- İade: kümülatif fiyatlama (`AdjustmentCalculator.RefundAmount`); bir kalemin tüm adetleri kaç parçada iade edilirse edilsin toplam tam kalem payı. Eski yöntem her adedi ayrı yuvarlıyordu: örnek hesap (indirim 10,01) 240,00 iade ederdi, ödenen 239,99.
- KDV kırılımı: oran başına brüt = o orandaki indirimli kalemlerin toplamı + ek ücret payı; toplam ödenecek tutar (bahşişsiz) ve vergi toplamı kuruşu kuruşuna tutar.

## Kanıt
- Birim: AdjustmentsDomainTests 25/25 (kesin kuruş değerleri; ek ücret ve bahşişli KDV kırılımı dahil); gerçek Postgres: Billing host 78/78 (PaymentRefundHttpTests dahil, yeni `RefundingEveryItemOfADiscountedBillInPartsGivesBackExactlyWhatWasPaid`: 96,00 + 95,99 + 48,00 = 239,99).
- Mutasyon (mutations-r1.txt, mutations.txt): M1-M7 (ilk tur) ve inceleme sonrası güçlenen testlerle M1-M4, M7-M10 (mutations-r3.txt) hepsi öldü; M3 ve M7 ilk turda yaşamıştı.

## Karar boşluğu / açık risk
- Görevdeki "kuruş kalıntısı son kalemde" yerine mevcut yardımcının en büyük kalıntı kuralı kullanıldı (toplam aynı, hiçbir kalem payından 1 kuruştan fazla sapmaz). Ürün isterse son kaleme çevrilir.
- İade kaydı KDV tutarı saklamıyor; iade KDV'si ayrı bir görevdir (mevzuat/e-fatura iade belgesi kararı gerekir).
- Kalem bazlı bölme (V1-RMD-413 reddi) bu görevde açılmadı; indirimli hesapta kalem bazlı bölme hâlâ 409.

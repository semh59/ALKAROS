# V1-RMD-982 sonuc

Hesap tasarimi yanitindaki kalemlere (BillSplitItemDto) addedWithoutKitchenReason alani eklendi; BillingSplitStore.MapAsync hesap kalemlerinin siparis kalemlerinden neden kodunu okur (yalniz kasada eklenenlerde dolu). Test: TheBillShowsTheReasonOnTheKitchenLessLineOnly. Billing testlerinden BillingSplitHttpTests + FromOrder + yeni test 77 gecti (green.log). Mutant: neden hic uygulanmazsa test kirilir.
Acik, ayri gorev: gun sonu raporunda mutfaksiz ekleme sayisi ve tutari (BusinessDayTotalsReader tek SQL ve kapanista saklanan sekil; BillOrderLinkCopiesTests master'da zaten kirik).

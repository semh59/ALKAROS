# V1-RMD-736 - Durum makinesi ve kapanış (603 B019, B020, B030, B040)

**Sonuç:** Tam iade sonrası Paid→Cancelled geçişi artık geçiş matrisinde var; ödenmiş ama kapanamamış hesapları dakikada bir yeniden kapatan süpürücü eklendi; iptal edilen istek 499, kilit/deadlock hatası 503 `BILL_BUSY` (Türkçe "tekrar deneyin") dönüyor; denetimsiz finansal satır silen `RemoveAsync` kaldırıldı. 6/6 mutasyon KILLED.

- **B019 (kısmen):** matris düzeltildi (`Bill.CanTransitionTo`), test `ASettledBillThatIsRefundedInFullCanBeCancelled`. Reopened/PartiallyPaid atanmıyor tespiti kapsamdaydı ama davranış değişikliği ürün kararı gerektirdiği için yapılmadı; iptal/iade/bölme ham SQL'i ve olay yayını yokluğu aynen duruyor.
- **B020 (doğru, düzeltildi):** `BillClosureSweeper` (1 dk, en fazla 200 hesap; kapanan hesabın siparişini de tamamlar). Kapanamayan hesap bir sonraki turda yeniden denenir. Test: tam ödenmiş ama Open'a çekilmiş hesap kapanır, kısmi ödenmiş olan dokunulmaz.
- **B030 (kısmen):** yalnız `RemoveAsync` silindi (finansal satırı audit'siz silen tek yöntem). `SaveSplitDesignAsync`, `DeleteSplitDesignAsync`, `GetByTableIdAsync`, `GetTotal*Async`, `CreateMergedBill/CreateSplitBills` testlerde ve test kurulumlarında yaygın kullanıldığı için bırakıldı.
- **B040 (kısmen):** 499 ve 40P01/55P03→503 yapıldı. İkinci bağlantı, kapanışta çift okuma ve `cash_sessions` indeksi ölçüm/migration gerektirdiğinden yapılmadı.

## Kanıt (logs/)
Host Billing 92/92, PaymentTender 55/55, modül BillFoundation 45/45, Adjustments 18/18, `mutations.log` 6/6 KILLED.

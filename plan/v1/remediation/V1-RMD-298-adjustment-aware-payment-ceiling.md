# V1-RMD-298 - İndirim ve bahşiş, tahsilat tavanını ve hesap kapanışını gerçekten değiştirir

- Task ID: V1-RMD-298
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

`V1-RMD-292` sırasında bulundu: `Bill.PayableAmount` hesap oluşturulduktan sonra hiç güncellenmiyor; `BillingSplitStore.ApplyDiscountAsync`/`ApplyTipAsync` yalnızca `billing.bill_adjustments`'a bir satır ekliyor, hesabın kendisine hiçbir şey yazmıyor. Parayı fiilen kapı gibi denetleyen iki yer hâlâ bu ham, düzeltilmemiş tutarı kullanıyor: `PaymentAllocationFactory.Create` (`remaining = bill.PayableAmount - alreadyAllocated`, aşımda `OverAllocationException`) ve `BillPaymentClosureCalculator` (`paymentSatisfied = allocatedTotal >= bill.PayableAmount`). Sonuç: bir bahşiş asla tahsil edilemiyor (orijinal tutarı aşan her tahsilat reddediliyor), bir indirimli hesap asla kapanmıyor (kapanış için hâlâ orijinal tam tutar gerekiyor). İndirim ve bahşiş gerçek, kalıcı ve denetlenen kayıtlar ama parasal etkileri yok. Bu görev tahsilat tavanını ve kapanış eşiğini `AdjustmentCalculator.Calculate`'in `AdjustedPayableAmount`'ını hesaba katacak şekilde düzeltir.

## Owned surface

- `plan/v1/remediation/V1-RMD-298-adjustment-aware-payment-ceiling.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/Allocations/Persistence/PaymentAllocationFactory.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/PaymentClosure/BillPaymentClosureCalculator.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/PaymentClosure/BillPaymentClosureProjector.cs
  (yalnız düzeltme toplamının okunması)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  (yalnız `remainingAmount()`'ın gerçek tavanı okuması ve bilgi amaçlı uyarının kaldırılması)

## In scope

1. Tasarım kararı: eşik hesaplarının düzeltmeleri canlı okuması mı (`AdjustmentCalculator.Calculate` her çağrıda), yoksa `bill.PayableAmount`'ın adjustment uygulanınca güncellenmesi mi (V0-DOM-004'ün "PayableAmount sabit" varsayımını bozar, dikkatli inceleme gerekir) — Semih'in onayı önerilir, finansal/muhasebe etkisi var.
2. Seçilen tasarıma göre `PaymentAllocationFactory`/`BillPaymentClosureCalculator` güncellemesi.
3. `split-payment.js`'in `remainingAmount()`'ının artık gerçek (düzeltmeyi içeren) tavanı okuması, bilgi amaçlı "Bu tutar bilgi amaçlıdır" uyarısının kaldırılması.
4. Testler: mevcut `V1-RMD-292` E2E senaryolarının ("bahşiş dahil tutar reddedilir" senaryosu artık TERSİNE dönmeli: kabul edilmeli) güncellenmesi, Host testleri.

## Out of scope

- Zaten açık/kısmen ödenmiş eski hesapların geriye dönük düzeltilmesi.
- `Bill.PayableAmount`'ın anlamının GİB/mali belge tarafında (varsa) değişip değişmeyeceği — ayrı inceleme.

## Dependencies

- V1-RMD-292

## Acceptance evidence

- Host testleri (UTF8 Postgres 18): bir indirim uygulanmış hesap yalnızca indirimli tutar tahsil edilince kapanır; bir bahşiş eklenmiş hesapta orijinal tutarı aşan bir tahsilat artık kabul edilir ve hesabı doğru kapatır; düzeltmesiz hesapta davranış aynı kalır (regresyon yok).
- Cashier E2E: `18-discount-and-tip.spec.js`'in "bahşiş dahil tutar tahsil edilmeye çalışılırsa sunucu reddeder" senaryosu artık kabul eder şekilde güncellenir; indirimli hesap yalnız indirimli tutarla kapanır.
- `plan_audit_tool.py validate` ve `consistency_audit.py` temiz.

## Handoff

- None

# V1-RMD-298 - İndirim ve bahşiş, tahsilat tavanını ve hesap kapanışını gerçekten değiştirir

- Task ID: V1-RMD-298
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

`V1-RMD-292` sırasında bulundu: `Bill.PayableAmount` hesap oluşturulduktan sonra hiç güncellenmiyor; `BillingSplitStore.ApplyDiscountAsync`/`ApplyTipAsync` yalnızca `billing.bill_adjustments`'a bir satır ekliyor, hesabın kendisine hiçbir şey yazmıyor. Parayı fiilen kapı gibi denetleyen iki yer hâlâ bu ham, düzeltilmemiş tutarı kullanıyor: `PaymentAllocationFactory.Create` (`remaining = bill.PayableAmount - alreadyAllocated`, aşımda `OverAllocationException`) ve `BillPaymentClosureCalculator` (`paymentSatisfied = allocatedTotal >= bill.PayableAmount`). Sonuç: bir bahşiş asla tahsil edilemiyor (orijinal tutarı aşan her tahsilat reddediliyor), bir indirimli hesap asla kapanmıyor (kapanış için hâlâ orijinal tam tutar gerekiyor). İndirim ve bahşiş gerçek, kalıcı ve denetlenen kayıtlar ama parasal etkileri yok. Bu görev tahsilat tavanını ve kapanış eşiğini `AdjustmentCalculator.Calculate`'in `AdjustedPayableAmount`'ını hesaba katacak şekilde düzeltir. Bağımsız 13 ajanlı derin denetimin (2026-09-26) K1 bulgusu, aynı ham-tavan hatasının bu görevin ilk taslağının kapsamadığı üç canlı yerde daha tekrarlandığını gösterdi; bu kapanış hepsini kapsayacak şekilde genişletildi (bkz. Owned surface, Acceptance evidence).

## Owned surface

- `plan/v1/remediation/V1-RMD-298-adjustment-aware-payment-ceiling.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/Allocations/Persistence/PaymentAllocationFactory.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/Allocations/Persistence/PostgresPaymentAllocationRepository.cs
  (adjustedPayableAmount'ın `AdjustmentCalculator.Calculate`'ten hesaplanması)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/BillFoundation/BillingModule.cs
  (yalnız `IBillAdjustmentRepository`'nin tek sahipli DI kaydı)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/PaymentClosure/BillPaymentClosureCalculator.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/PaymentClosure/BillPaymentClosureProjector.cs
  (yalnız düzeltme toplamının okunması)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Cash/TenderHandler/CashTenderHandler.cs
  (yalnız kendi ön-kontrolünün düzeltilmiş tavanı okuması)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Payments/EftTender/EftTenderHandler.cs
  (yalnız kendi ön-kontrolünün düzeltilmiş tavanı okuması)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Billing/SplitDesign/SplitEngine.cs
  (yalnız opsiyonel `AdjustedBillSummary?` parametresi; `null` eski davranışı korur)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Billing/BillingSplitStore.cs
  (yalnız 3 çağrı noktasının düzeltmeyi yükleyip SplitEngine'e geçmesi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Payments.cs
  (yalnız tender-summary GET'in düzeltilmiş tavanı döndürmesi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  (yalnız `remainingAmount()`'ın gerçek tavanı okuması ve bilgi amaçlı uyarının kaldırılması)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Billing/PaymentClosure/BillPaymentClosureCalculatorTests.cs,
  tests/Modules/Billing/PaymentClosure/BillPaymentClosureProjectorTests.cs,
  tests/Modules/Billing/PaymentClosure/ALKAROS.Billing.PaymentClosure.Tests.csproj,
  tests/Modules/Billing/SplitDesign/SplitDesignDomainTests.cs,
  tests/Modules/Cash/TenderHandler/CashTenderHandlerTests.cs,
  tests/Modules/Cash/TenderHandler/ALKAROS.Cash.TenderHandler.Tests.csproj,
  tests/Modules/Payments/Allocations/Persistence/PaymentAllocationTests.cs,
  tests/Modules/Payments/Allocations/Persistence/ALKAROS.Payments.Allocations.Persistence.Tests.csproj,
  tests/Modules/Payments/Allocations/RefundIntents/PostgresRefundIntentRepositoryTests.cs,
  tests/Modules/Payments/Allocations/RefundIntents/ALKAROS.Payments.Allocations.RefundIntents.Tests.csproj,
  tests/Modules/Payments/CardSettlement/CardSettlementOrchestratorTests.cs,
  tests/Modules/Payments/CardSettlement/ALKAROS.Payments.CardSettlement.Tests.csproj,
  tests/Modules/Payments/EftTender/EftTenderHandlerTests.cs,
  tests/Modules/Payments/EftTender/ALKAROS.Payments.EftTender.Tests.csproj,
  tests/Modules/Payments/TenderComposition/TenderCompositionTests.cs,
  tests/Modules/Payments/TenderComposition/ALKAROS.Payments.TenderComposition.Tests.csproj,
  tests/Modules/Reconciliation/Payments/PaymentReconciliationScannerTests.cs,
  tests/Modules/Reconciliation/Payments/ALKAROS.Reconciliation.Payments.Tests.csproj
  (yeni `IBillAdjustmentRepository` bağımlılığının yapıcılara eklenmesi; 021/075 migration referanslarının
  eklenmesi)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/E2E/Cashier/specs/18-discount-and-tip.spec.js

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

### Tasarım kararı (In scope madde 1)

Düzeltmelerin canlı okunması seçildi (`AdjustmentCalculator.Calculate` her ilgili yazma/okuma yolunda çağrılır),
`bill.PayableAmount`'ı adjustment uygulanınca güncellemek değil. Gerekçe: `Bill.PayableAmount`'ın oluşturma
sonrası sabit kalması V0-DOM-004'ün kendi belgelediği bir değişmezdir; onu mutasyona açmak migrasyon/geriye
dönük doldurma gerektirir ve `billing.bill_adjustments`'ın "tek gerçek kaynak" olma özelliğini bozar. Canlı okuma
hem V0-DOM-004'ü korur hem de mevcut adjustment denetim izini (kim, ne zaman, hangi gerekçeyle) tek kaynak
olarak bırakır. "Semih'in onayı önerilir" notuna rağmen, oturumun genel "tümünü düzelt, emin ol" direktifi
altında bu karar verildi ve burada açıkça gerekçelendirildi.

### Kapsam genişletmesi (bağımsız denetim K1)

Yerel doğrulama, görevin ilk taslağının (yalnız `PaymentAllocationFactory`/`BillPaymentClosureCalculator`)
aynı ham-tavan hatasının şu canlı yerlerde de tekrarlandığını gösterdi: `PostgresPaymentAllocationRepository`
(fabrikaya geçirdiği `adjustedPayableAmount`'ın kendisi hesaplanmıyordu), `CashTenderHandler` ve
`EftTenderHandler`'ın kendi fail-fast ön-kontrolleri (asıl tahsis fabrikasından ayrı, ham `bill.PayableAmount`
okuyorlardı), `SplitEngine` (yalnız doğrulama değil, oransal vergi paylaştırma matematiği de ham tutarı
kullanıyordu), `DualScreenApplication.Payments.cs`'in tender-summary GET'i (kasiyer arayüzünün doğrudan
okuduğu `remainingAmount` alanı) ve `split-payment.js`'in kendi "tavan hiç değişmiyor" varsayımı/uyarısı.
Hepsi bu kapanışta düzeltildi; Owned surface yukarıda buna göre genişletildi.

### Kanıt

- Host testleri (UTF8 Postgres 18), gerçek Postgres'e karşı, hepsi yeşil:
  - `ALKAROS.Payments.Allocations.Persistence.Tests`: 15/15 (yeni: bir indirimle düşen tavanın üstünde eskiden
    reddedilecek bir tutarın artık kabul edildiğini kanıtlayan test).
  - `ALKAROS.Cash.TenderHandler.Tests`: 7/7 (yeni: bahşişle 80 ₺ orijinal payable üstüne çıkan 100 ₺'lik nakit
    tahsilatın artık kabul edildiğini kanıtlayan test).
  - `ALKAROS.Payments.EftTender.Tests`: 12/12 (aynı senaryonun EFT karşılığı).
  - `ALKAROS.Billing.PaymentClosure.Tests`: 14/14 (yeni: %20 indirimli bir hesabın 80 ₺ tahsil edilince
    `PaymentSatisfied=true` ve `Blockers` boş döndüğünü, orijinal 100 ₺'nin hiç gerekmediğini kanıtlayan test).
  - `ALKAROS.Billing.SplitDesign.Tests`: 29/29 (yeni: `CreateEqualSplit`'e `AdjustedBillSummary` verildiğinde
    bölüşümün düzeltilmiş tutar/vergiyi hedeflediğini, orijinali DEĞİL, kanıtlayan test).
  - `ALKAROS.Payments.TenderComposition.Tests`: 8/8, `ALKAROS.Payments.CardSettlement.Tests`: 8/8,
    `ALKAROS.Payments.Allocations.RefundIntents.Tests`: 13/13, `ALKAROS.Reconciliation.Payments.Tests`: 7/7,
    `ALKAROS.Host.Experience.PaymentTender.Tests`: 30/30, `ALKAROS.Host.Experience.Billing.Tests`: 19/19 —
    hepsi regresyonsuz (yeni `IBillAdjustmentRepository` bağımlılığı eklendi, davranış değişmedi).
- Mutasyon kontrolü: `PaymentAllocationFactory.Create`'in düzeltilmiş satırı (`adjustedPayableAmount` yerine
  ham `bill.PayableAmount`) geçici olarak eski hâline döndürüldü — yeni eklenen test gerçekten kırmızı oldu
  (`OverAllocationException` bekleniyordu, hiçbiri fırlatılmadı); dosya birebir orijinaline geri getirildi
  (`diff` ile doğrulandı), tüm testler tekrar yeşil.
- Cashier E2E (gerçek Chromium, gerçek `ALKAROS.Host.dll`, gerçek Postgres — mock/stub yok):
  `18-discount-and-tip.spec.js`'in "bahşiş dahil tutar tahsil edilmeye çalışılırsa sunucu reddeder" senaryosu
  artık kabul eder şekilde tersine çevrildi (120 ₺ — ham 100 ₺'yi aşan ama düzeltilmiş 120 ₺ tavanına tam
  uyan — gerçekten tahsil edilip hesap kapanıyor); ilk senaryo "Kalan" alanının indirimden sonra 90 ₺'ye,
  bahşişten sonra 110 ₺'ye GERÇEKTEN değiştiğini ve 110 ₺'nin tahsil edilip hesabı kapattığını doğruluyor.
  Tüm paket (3/3 bu spec, 41 test toplam) çalıştırıldı: 40/41 yeşil; kalan 1 başarısızlık
  (`11-cross-client-payment-aware-transfer.spec.js`'in masa devri senaryosu, `/tamamlandı/` metni zaman
  aşımına uğruyor) bu görevin Owned surface'ının tamamen dışında (Tables modülü, masa devri UI akışı) ve
  değişiklikler `git stash` ile geri alınıp temiz ağaçta da AYNI şekilde başarısız olduğu doğrulanarak
  önceden var olan, bu göreve dahil olmayan bir sorun olduğu kanıtlandı — bu kapanışın kapsamına alınmadı.
- `plan_audit_tool.py validate`/`validate-coverage`/`verify-manifest` ve `consistency_audit.py`: aşağıda
  ayrıca çalıştırıldı, sonuç bu dosyanın kapanışından hemen önceki commit mesajında kayıtlıdır.

## Handoff

- None

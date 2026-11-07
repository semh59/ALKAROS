# V1-RMD-898 - Ödeme sonrası indirimde eski iadelerin eski tutardan kalması şüphesi: önce davranışı çalıştıran test

- Task ID: V1-RMD-898
- Status: Done
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

Kalem iadesi, kalemin indirimli payını iade anındaki ayarlamalardan hesaplar; daha önce yapılmış iadeler ise kaydedildikleri tutarda kalır. Şüphe: bir kalem kısmen iade edildikten sonra hesaba (tahsil edilenin altına inmeyen) indirim uygulanırsa, sonraki iade yeni paydan hesaplandığı için bir kalemin toplam iadesi ödenen tutarla uyuşmaz (fazla ya da eksik iade). Önce davranışı çalıştıran kırmızı test yazılır: kısmen ödenmiş hesapta bir kalem kısmen iade edilir, sonra indirim uygulanır, kalan adetler iade edilir; toplam iade, o kalemin gerçekte ödenen payıyla karşılaştırılır. Şüphe kısmen gerçek çıktı: kalan adetlerin yeni indirimli payı iade edilebilir (897 sonrası toplam doğru), ancak ödemenin kalanı yeni fiyatla gerçekte ödenecek tutardan küçükken, iade tutarı değil kalemin ham fiyatı kalanla karşılaştırıldığı için geçerli iade REFUND_AMOUNT_EXCEEDS_PAYMENT ile reddediliyor; düzeltilir. Değilse testler kanıt olarak kalır ve RESULT.md'ye çürütme gerekçesi yazılır.

## Owned surface

- `evidence/V1-RMD-898/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - tests/Host/Experience/Billing/RefundAfterDiscountHttpTests.cs — yeni test
  - src/Host/Experience/Billing/PaymentRefunds.cs
  - src/Clients/PosTerminal/src/features/billing/refundApi.ts
  - src/Clients/PosTerminal/src/features/billing/refundApi.test.ts
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-cancel-order.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı/yeşil test: iade, sonra indirim, sonra kalan adetlerin iadesi; ödemeyle iade toplamlarının ilişkisi
2. Düzeltme: aşım denetimi, önceki iadelerin fazla ödediği tutarı (indirim sonrası) hesaba katar
3. İndirimle fazla ödenmiş önceki iade yüzünden kalan kalem iadesi reddedilirse sıfır-tutar mesajı yerine nedeni anlatan Türkçe hata (REFUND_EARLIER_OVERPAID) gösterilir (sunucu, React kasa, Cashier)
4. İki bağımsız qa incelemesi

## Out of scope

- Kalem olmayan (tutar) iadeler ve indirim kuralları (DISCOUNT_BELOW_COLLECTED) değişmez

## Dependencies

- V1-RMD-897

## Acceptance evidence

1. Test çıktıları ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

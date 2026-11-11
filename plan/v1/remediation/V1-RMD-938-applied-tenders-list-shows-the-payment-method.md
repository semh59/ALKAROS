# V1-RMD-938 - Uygulanan tahsilatlar listesi ödeme yöntemini göstersin

- Task ID: V1-RMD-938
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu (G14): ödeme ekranında "Uygulanan tahsilatlar" satırları yalnız tutarı ve "Onaylandı" yazısını gösteriyor; nakit mi, kart mı, EFT mi, cari mi olduğu görünmüyor. Karar: tahsilat satırı yöntemi sunucudaki tek tanımdan (PaymentMethodLookup, fişle ve iadeyle aynı) okuyup gönderir, ekran satırda Türkçe yöntem adını gösterir.

## Owned surface

- `evidence/V1-RMD-938/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenApplication.Payments.cs
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Host/Experience/PaymentTender/PaymentTenderHttpTests.cs
  - tests/Clients/StaticApps/split-payment-display-binding.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı sunucu ve ekran testi, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Satır durumunun (Onaylandı) gerçek duruma bağlanması

## Dependencies

- V1-RMD-937

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

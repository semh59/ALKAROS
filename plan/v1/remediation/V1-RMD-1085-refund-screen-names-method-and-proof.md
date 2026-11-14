# V1-RMD-1085 - İade ekranı yöntemi ve gerçek kanıt adımını söyler

- Task ID: V1-RMD-1085
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasa kullanıcı denemesi 10: Havale/EFT iadesinde başlık "iade dekontu girişi bekleniyor" diyor ama dekont girişi alanı yok (arka uç EFT tamamlamada fiş numarası göndermez, alan uydurulmaz); ürün iadesinde ödeme seçeneği "Ödeme · tutar" yöntemi söylemiyor. Başlık gerçek adımı (havale gönderimi) söyler, kart için fiş girişi korunur; ödeme seçeneği etiketi Türkçe yöntem adını taşır.

## Owned surface

- `evidence/V1-RMD-1085/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-open-refund-closed.test.js
  - tests/Clients/StaticApps/split-payment-item-refund.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar; arka uç sözleşmesi

## Dependencies

- V1-RMD-1084

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi

2. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

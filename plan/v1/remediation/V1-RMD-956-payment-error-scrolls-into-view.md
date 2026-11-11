# V1-RMD-956 - Ödeme hatası yapışkan düğmenin arkasında kalmasın

- Task ID: V1-RMD-956
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu: hatalı tutar girilince hata metni sayfanın tepesinde çıkıyor, ekranın altındaki yapışkan Öde düğmesinin başındaki kasiyer onu göremiyor. Karar: ekran her yeni hata metninde hata kutusunu görünür alana kaydırır.

## Owned surface

- `evidence/V1-RMD-956/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-change.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-955

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

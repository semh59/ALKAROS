# V1-RMD-948 - Ödenmiş hesap sayfası, yöneticiye özel fiş sıfırlamayı kasiyerden gizlesin

- Task ID: V1-RMD-948
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu (G14): ödeme sonrası sayfada kasiyer "Orijinal fişi sıfırla (yönetici)" kartını görüyor; sunucu bu işlemi yalnız iade onay yetkisine (payments.refund.approve) veriyor, kasiyerin kartı kullanması reddedilir ve sayfa gereksiz kalabalık. Karar: oturumun yetki listesinde payments.refund.approve yoksa sıfırlama kartı hiç çizilmez; yetkili yönetici için aynen kalır.

## Owned surface

- `evidence/V1-RMD-948/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-receipt-reset.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-947

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

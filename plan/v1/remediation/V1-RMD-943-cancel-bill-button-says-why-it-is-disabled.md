# V1-RMD-943 - "Hesabı iptal et" düğmesi pasifken nedenini söylesin

- Task ID: V1-RMD-943
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu (G14): iptal gerekçesi seçilmeden "Hesabı iptal et" düğmesi pasif kalıyor ve ekranda nedeni yazmıyor. Karar: gerekçe boşken düğmenin üstünde "Hesabı iptal etmek için bir gerekçe seçin ya da yazın." yazar, gerekçe girilince kaybolur.

## Owned surface

- `evidence/V1-RMD-943/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-cancel-order.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-942

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

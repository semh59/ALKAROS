# V1-RMD-1009 - Kişi başı tutar düğmesi ödeme yöntemini sıfırlamaz

- Task ID: V1-RMD-1009
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Ödeme ekranında Kayıtlı dağılım (kişi başı) düğmesine basınca seçili ödeme yöntemi boşalıyor ve her kişide yöntem yeniden seçilmek zorunda kalıyordu: düğme, ödeme yöntemi düğmeleriyle aynı sınıfı taşıdığından yöntem işleyicisi ona da bağlanıp yöntemi boş değere ayarlıyordu. Kişi düğmesi yalnız tutarı doldurur, seçili yöntem ve Eşit bölüştür ilerlemesi korunur.

## Owned surface

- `evidence/V1-RMD-1009/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-table-note.test.js
  - tests/Clients/StaticApps/split-payment-hierarchy.test.js
  - tests/Clients/StaticApps/split-payment-quick-cash.test.js
  - tests/Clients/StaticApps/split-payment-person-method.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1008

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

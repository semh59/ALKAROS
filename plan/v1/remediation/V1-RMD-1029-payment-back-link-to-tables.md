# V1-RMD-1029 - Masa hesabında Kasaya dön bağlantısı masa planına gider

- Task ID: V1-RMD-1029
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Ödeme ekranındaki Kasaya dön bağlantısı her zaman satış ekranına (/) gidiyordu; masa hesabını ödeyen kasiyer masa planına dönmek için bir dokunuş daha harcıyordu. Hesap bir masaya aitse bağlantı /tables adresine, hızlı satışta satış ekranına gider.

## Owned surface

- `evidence/V1-RMD-1029/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-table-note.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1028

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

# V1-RMD-972 - Credit limit 0 says clearly that credit cannot be written

- Task ID: V1-RMD-972
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

A customer with credit limit 0 cannot be charged to the account (product decision: stays that way). The cashier cari page and the split-payment customer picker must say so plainly and who can change the limit.

## Owned surface

- `evidence/V1-RMD-972/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/customer-accounts/customer-accounts.js
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/customer-accounts-flows.test.js
  - tests/Clients/StaticApps/split-payment-credit-limit.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-971

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

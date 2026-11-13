# V1-RMD-1034 - "Kasada eklendi" notu müşteri fişinde görünmez, kasiyer ekranında kalır

- Task ID: V1-RMD-1034
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Mutfaksız eklenen satırın "Kasada eklendi" notu müşteriye verilen Bilgi Fişi'nde (split-payment.js receiptHtml) her satırın altında basılıyordu; müşteri bunu görmemeli. Not yalnız kasiyer ekranlarında (ödeme ekranı bağlam notu, masa paneli, bekleyen hesaplar) ve iç raporda kalır. Müşteri ekranı satırı zaten yalnız izin listesindeki alanlarla (CustomerDisplayContractTests) taşır; bu görev onu değiştirmez, mevcut testi kanıt olarak koşar. PO kararı 2026-11-13 (C).

## Owned surface

- `evidence/V1-RMD-1034/**`
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

- V1-RMD-1033

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

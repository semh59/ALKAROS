# V1-RMD-1008 - Ödeme ekranında Ödemeyi Ekle düğmesi 768 piksel yükseklikte ilk ekranda

- Task ID: V1-RMD-1008
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Hesap ödeme ekranında Ödemeyi Ekle düğmesi 1366x768 ve 1024x768 ekranlarda ilk ekranın altında kalıyordu (üstü 775 px). Nakit alanındaki hızlı tutar düğmeleri tek satıra, tutar ve özet satırları sıkılaştırılır; masa adı ve mutfaksız not satırı varken de düğme ilk ekranda görünür.

## Owned surface

- `evidence/V1-RMD-1008/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css
  - tests/Clients/StaticApps/split-payment-table-note.test.js
  - tests/Clients/StaticApps/split-payment-hierarchy.test.js
  - tests/Clients/StaticApps/split-payment-quick-cash.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1007

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

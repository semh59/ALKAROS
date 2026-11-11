# V1-RMD-940 - Ödeme sayfası bir ödeme yöntemini tahsilat ve iade satırlarında aynı adla yazsın

- Task ID: V1-RMD-940
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-938 incelemesinden: tahsilat satırı yöntemi düğme adıyla (Kredi/Banka Kartı, EFT/Havale, Hesaba yaz (cari)), iade satırı ve fiş ise ad olarak (Kart, Havale / EFT, Cari hesap) yazıyor; aynı ödeme iki ekranda iki adla görünüyor. Karar: bir ad sözlüğü (fiş ve iadeyle aynı: Nakit, Kart, Havale / EFT, Cari hesap) hem tahsilat hem iade satırında kullanılır; yöntem seçim düğmeleri eylem adı olarak kalır.

## Owned surface

- `evidence/V1-RMD-940/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-display-binding.test.js
  - tests/Clients/StaticApps/split-payment-refunds.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-939

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

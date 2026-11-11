# V1-RMD-939 - Uygulanan tahsilat satırı gerçek ödeme durumunu göstersin

- Task ID: V1-RMD-939
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-938 incelemesinden: ödeme ekranındaki tahsilat satırı her durumda sabit "Onaylandı" yazıyor; iade edilmiş veya kısmen iade edilmiş ödeme de onaylı görünüyor. Karar: satır sunucunun gönderdiği ödeme durumunu Türkçe ad olarak gösterir (sunucudaki PaymentStatusText ile aynı adlar); bilinmeyen durum "Bilinmiyor" der, yalnız onaylı durum yeşil rozet alır.

## Owned surface

- `evidence/V1-RMD-939/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-display-binding.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-938

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

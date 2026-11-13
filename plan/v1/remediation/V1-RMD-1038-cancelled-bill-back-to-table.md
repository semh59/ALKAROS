# V1-RMD-1038 - Hesap iptalinden sonra sipariş masada kaldıysa ödeme ekranı masaya dönüş düğmesi verir

- Task ID: V1-RMD-1038
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer hesabı iptal edip siparişi iptal etmediğinde sipariş masada kalır; "Hesap İptal Edildi" ekranında şimdiye dek yalnız "Bekleyen hesaplara dön" vardı. Hesap bir masaya aitse ve sipariş de iptal edilmediyse ekran ek olarak "Masaya dön" düğmesi gösterir (masa planına gider); sipariş birlikte iptal edildiyse masa boşaldığı için gösterilmez. PO talebi: kasiyer denemesi 6, sorun 3.

## Owned surface

- `evidence/V1-RMD-1038/**`
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

- V1-RMD-1037

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

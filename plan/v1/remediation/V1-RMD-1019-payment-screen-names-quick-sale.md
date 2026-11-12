# V1-RMD-1019 - Hızlı satış tahsilat ekranı yerini Hızlı satış diye yazar

- Task ID: V1-RMD-1019
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Masasız (hızlı satış) hesabın tahsilat ve ödendi ekranında hesabın yeri hiç yazmıyordu; masa hesabında Masa M-10 yazıyor. Hesap bilgisi okunabilmiş ve masa yoksa ekran Hızlı satış yazar; hesap bilgisi okunamadıysa hiçbir yer yazılmaz.

## Owned surface

- `evidence/V1-RMD-1019/**`
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

- V1-RMD-1018

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

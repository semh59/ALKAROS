# V1-RMD-1076 - Kapanmış hesapta açılan iade isteği "hesap kapanmaz" demez

- Task ID: V1-RMD-1076
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Ödeme ekranı, onay bekleyen iade isteği olan hesapta "Tahsilat Tamam, Hesap Açık ... karar verilene kadar hesap kapanmaz" cümlesini gösterir (V1-RMD-1037); bu cümle yalnız hesap kapanıştan önce açılan istekte doğrudur. Hesap zaten kapanmışsa (fiş okumasındaki durum alanı Paid) ekran "Hesap Ödendi" başlığı altında "İade isteği gönderildi, yönetici kararı bekleniyor." yazar; kapanmamış hesapta mevcut cümle aynen kalır. PO kararı Semih 2026-11-14 (kasiyer denemesi 9).

## Owned surface

- `evidence/V1-RMD-1076/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-open-refund-closed.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. tests/Clients/StaticApps tam koşusu

## Out of scope

- Listede olmayan ekranlar ve dosyalar; AwaitingManualRefund durumundaki (onaylanmış) istekler

## Dependencies

- V1-RMD-1037

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. tests/Clients/StaticApps tam koşusu exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

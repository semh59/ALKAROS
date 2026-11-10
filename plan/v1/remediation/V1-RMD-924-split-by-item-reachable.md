# V1-RMD-924 - Ürüne ve kişiye göre bölme ödeme sayfasından açılır

- Task ID: V1-RMD-924
- Status: Done
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi (G14, rapor 12, bulgu 4): ödeme sayfasında yalnız "Eşit bölüştür" var; ürün/kişi/tutar bazlı "Hesap bölme" ekranı (`/billing`) hiçbir ekrandan açılamıyor, menüden girince "Aktif adisyon yok" boş ucu çıkıyor. Ekran `?billId=` ile hesabı açabiliyor ama ona giden bağlantı yok. Karar: ödeme sayfasında, bölme bölümünün yanında hesabın kimliğiyle `/billing?billId=...` bağlantısı ("Ürüne veya kişiye göre böl") gösterilir (kart kilidi varken gizli, çünkü form da gizli).

## Owned surface

- `evidence/V1-RMD-924/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-display-binding.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test: bağlantı hesabın kimliğiyle görünür, kilitliyken görünmez
2. Düzeltme, mutasyon, iki bağımsız qa

## Out of scope

- Bölme ekranının iç işleyişi, kart kilidi (V1-RMD-923 karar bekliyor)

## Dependencies

- V1-RMD-922

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

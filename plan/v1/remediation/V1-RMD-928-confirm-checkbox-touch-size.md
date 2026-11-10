# V1-RMD-928 - Ödeme ve kasa kapanışı onay kutuları dokunmaya uygun boyutta

- Task ID: V1-RMD-928
- Status: Done
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi (G14, bulgu 8): "Siparişi de iptal et" (ödeme sayfası, `.sp-check input`) ve kasa kapanışındaki "ödenmemiş hesapları bilerek kapat" (`.cs-check input`) onay kutuları 24x24 px; ıslak veya kalın parmakla basılamıyor. Aynı sayfadaki EFT onay kutusu zaten 48 px (`.sp-confirm-row`). Karar: iki kutu da `var(--touch-target)` (48 px) olur; mevcut dokunma hedefi testi bu iki kuralı da kapsar.

## Owned surface

- `evidence/V1-RMD-928/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css
  - src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.css
  - tests/Clients/StaticApps/cashier-touch-targets.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (iki kural dokunma hedefi listesine), düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Diğer ekranların kutuları

## Dependencies

- V1-RMD-927

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

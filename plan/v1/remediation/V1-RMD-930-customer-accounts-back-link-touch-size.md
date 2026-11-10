# V1-RMD-930 - Cari hesaplar sayfasındaki "Kasaya dön" bağlantısı dokunmaya uygun boyutta

- Task ID: V1-RMD-930
- Status: Done
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi (G14, bulgu 8): cari hesaplar sayfasındaki "← Kasaya dön" bağlantısı yaklaşık 21 px yüksekliğinde; ödeme ve kasa oturumu sayfalarındaki karşılıkları 48 px. Karar: `.ca-back` en az 48 px yüksekliğinde olur ve dokunma hedefi testi bu kuralı kapsar.

## Owned surface

- `evidence/V1-RMD-930/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/customer-accounts/customer-accounts.css
  - tests/Clients/StaticApps/cashier-touch-targets.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Cari sayfasındaki diğer öğeler

## Dependencies

- V1-RMD-929

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

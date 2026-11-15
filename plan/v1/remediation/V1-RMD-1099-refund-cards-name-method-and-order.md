# V1-RMD-1099 - Kasa iade kartları ödeme yöntemini ve sırasını söyler

- Task ID: V1-RMD-1099
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasa kullanıcı denemesi 11: iade isteği öncesi ödeme kartları yalnız tutar gösteriyor ("₺100,00"); iki nakit ödemede (85 + 85) kartlar ve gerekçe alanları birbirinden ayırt edilemiyor. Kart başlığı ve gerekçe etiketi Türkçe yöntem adını taşır (Nakit · ₺85,00); aynı yöntemden birden çok ödeme varsa ödeme sırası eklenir (Nakit · ₺85,00 · 2. ödeme; sıra sunucunun verdiği ödeme zamanı sırasıdır). Ürün bazlı iade ödeme seçeneği aynı etiketi kullanır. Sunucu sözleşmesi değişmez.

## Owned surface

- `evidence/V1-RMD-1099/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-refunds.test.js
  - tests/Clients/StaticApps/split-payment-item-refund.test.js
  - tests/Clients/StaticApps/split-payment-refund-rules.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Arka uç sözleşmesi; ödeme zamanı alanı eklemek; istek listesi satırları

## Dependencies

- V1-RMD-1098

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0

3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

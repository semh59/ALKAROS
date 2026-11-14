# V1-RMD-1062 - Ödeme sayfasında hiçbir yerde kullanılmayan .sp-line-amount stil kuralı kaldırılır

- Task ID: V1-RMD-1062
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Hesap Ödeme sayfasının split-payment.css dosyasındaki .sp-line-amount kuralı hiçbir JS, HTML veya testte kullanılmıyor (V1-RMD-1059 qa incelemesi buldu); ölü kod sözleşmesi gereği kural silinir ve kullanılmadığını koruyan bir test eklenir.

## Owned surface

- `evidence/V1-RMD-1062/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css
  - tests/Clients/StaticApps/split-payment-cashier-rules.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1061

## Acceptance evidence

1. Test çıktıları ve mutant listesi
2. tests/Clients/StaticApps tam koşusu exit code 0
3. İki ardışık bağımsız qa-* incelemesi.

## Handoff

- None

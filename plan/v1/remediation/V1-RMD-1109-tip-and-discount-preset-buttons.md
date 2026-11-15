# V1-RMD-1109 - Bahşiş ve indirimde hazır yüzde düğmeleri

- Task ID: V1-RMD-1109
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu: bahşiş ve indirim için tutar elle hesaplanıp yazılıyor. Bahşiş bölümüne %10, %15, %20 düğmeleri eklenir (ödenecek tutar üzerinden hesaplanıp bahşiş tutar alanına yazar; ekleme için "Bahşiş ekle" yine ayrı basılır); indirim bölümüne %5, %10, %20 düğmeleri eklenir (hesaplama türünü yüzde yapar ve değeri yazar; "İndirim uygula" yine ayrı basılır, gerekçe ve sunucunun yetki/onay kuralı değişmez). Düğmeler hiçbir şeyi kendiliğinden göndermez.

## Owned surface

- `evidence/V1-RMD-1109/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-adjustment-presets.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- İndirim yetki ve onay kuralları; ikram; sunucu sözleşmesi

## Dependencies

- V1-RMD-1108

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

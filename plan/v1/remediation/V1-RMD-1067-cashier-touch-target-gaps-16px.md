# V1-RMD-1067 - Masa paneli ve Tahsilat sayfasında dokunma hedefleri arası boşluk en az 16 piksel olur

- Task ID: V1-RMD-1067
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

docs/CASHIER_UI_RULES.md 3 yan yana ve alt alta dokunma hedefleri arası en az 16px ister. Kasiyer denemesi 8 masa panelinde düğme satırları arası 12,8px (.table-order-bridge gap .8rem) ve Tahsilat sayfasında ödeme yöntemi düğmeleri arası 8px (.sp-method-chips; ayrıca .sp-row 10px ve .sp-quick-cash 8px) ölçtü. Bu boşluklar 16px'e çıkarılır; testler değerleri korur.

## Owned surface

- `evidence/V1-RMD-1067/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/styles.css
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.css
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
  - tests/Clients/StaticApps/split-payment-cashier-rules.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. tests/Clients/StaticApps tam koşusu
4. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build)

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1066

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

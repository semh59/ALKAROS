# V1-RMD-965 - Eşleştirme düğmesi hangi ekranı eşleştirdiğini söylesin

- Task ID: V1-RMD-965
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu: üst çubuktaki 'Ekranı eşleştir' düğmesi hangi ekranı eşlediğini söylemiyor ve pencerenin gönder düğmesiyle aynı etiketi taşıyor. Karar: üst çubuk düğmesi 'Müşteri ekranını eşleştir', penceredeki gönder düğmesi 'Eşleştir' olur; müşteri ekranındaki yönlendirme metni ve testler buna uyar.

## Owned surface

- `evidence/V1-RMD-965/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/CustomerDisplay.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.help-alerts.test.tsx
  - src/Clients/PosTerminal/src/routes/CustomerDisplay.test.tsx
  - src/Clients/PosTerminal/src/stale.test.ts
  - tests/E2E/Cashier/specs/03-screensaver.spec.js
  - tests/E2E/Cashier/specs/16-customer-display-refresh-on-payment.spec.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-964

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

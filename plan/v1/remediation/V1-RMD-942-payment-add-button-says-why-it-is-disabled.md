# V1-RMD-942 - "Ödemeyi Ekle" düğmesi pasifken nedenini söylesin

- Task ID: V1-RMD-942
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu (G14): havale/EFT veya cari yöntemi seçilince "Ödemeyi Ekle" düğmesi pasif kalıyor ama ekranda nedeni yazmıyor (onay kutusu işaretlenmemiş, müşteri seçilmemiş). Karar: düğmenin altında nedeni anlatan tek cümle çıkar; sebep kalkınca cümle kaybolur.

## Owned surface

- `evidence/V1-RMD-942/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
  - tests/Clients/StaticApps/split-payment-display-binding.test.js
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-941

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

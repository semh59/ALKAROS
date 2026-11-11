# V1-RMD-968 - Sepet başlığı eksik sipariş numarasında tür hatası vermesin

- Task ID: V1-RMD-968
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-962 orderTitle(orderNumber: string) yazdı; sunucu modeli orderNumber alanını string | null taşıyor ve tsc Cashier.tsx(866) için hata veriyor (vitest tür denetlemez). Karar: orderTitle null alır; numara yoksa 'Sipariş' yazar.

## Owned surface

- `evidence/V1-RMD-968/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.first-touch.test.tsx
  - evidence/V1-RMD-967/RESULT.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-967

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

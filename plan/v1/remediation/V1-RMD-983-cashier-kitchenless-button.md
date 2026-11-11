# V1-RMD-983 - Kasa ekraninda Mutfaga gondermeden hesaba ekle dugmesi

- Task ID: V1-RMD-983
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasa ekranında yetkili kasiyer, açık hesaba mutfağa göndermeden ürün ekleyebilir: "Mutfağa göndermeden hesaba ekle" düğmesi neden seçtirir (garson unuttu, müşteri istedi, diğer), seçimden sonra dokunulan ürünler mutfak fişi açılmadan hesaba eklenir; yetkisiz kasiyer düğmeyi görmez.

## Owned surface

- `evidence/V1-RMD-983/**`
- `src/Clients/PosTerminal/src/routes/Cashier.kitchenless.test.tsx`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/api.ts
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-982

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

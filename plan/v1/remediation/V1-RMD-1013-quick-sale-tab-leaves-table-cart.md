# V1-RMD-1013 - Hızlı satış sekmesi masa sepetini bırakır, seçili sekme doğru

- Task ID: V1-RMD-1013
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Masa sepetindeyken üstteki Hızlı satış sekmesi hem seçili görünüyor hem de tıklanınca hiçbir şey yapmıyordu; kasiyer masa sepetinden hızlı satışa geçmek için Masalar'a gidip geri dönmek zorundaydı. Masa sepetindeyken sekme seçili görünmez, tıklanınca masa sepetini bırakıp temiz hızlı satışa geçer.

## Owned surface

- `evidence/V1-RMD-1013/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.table-cart.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1012

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

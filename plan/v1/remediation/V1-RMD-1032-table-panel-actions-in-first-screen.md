# V1-RMD-1032 - Masa panelinin eylemleri 1024x768 ve 768x1024 ilk ekranda

- Task ID: V1-RMD-1032
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

1024x768'de Böl ve Masa taşı ilk ekranın dışında (y=891, ekran 768), 768x1024'te panel masaların altına inip Öde y=1244'te kalıyordu. Panelde eylem düğmeleri (Öde, Ürün ekle, Böl, Masa taşı, İptal / iade) kalem listesinin üstüne alınır ve 768 piksel genişlikte panel masaların yanında kalır (tek sütun yalnız 767 pikselin altında). Gerçek tarayıcıda getBoundingClientRect ile ölçülür.

## Owned surface

- `evidence/V1-RMD-1032/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
  - src/Clients/PosTerminal/src/features/tables/tables.css
  - src/Clients/PosTerminal/src/features/tables/tablesStyle.test.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1031

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

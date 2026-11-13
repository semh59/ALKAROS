# V1-RMD-1044 - Kasiyer masa panelinde kalemin mutfak durumunu (Hazır, Servis edildi) görür

- Task ID: V1-RMD-1044
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Masa panelindeki kalem listesi yalnız "Mutfağa gönderilmedi" ve "Kasada eklendi" notunu gösteriyordu; kasiyer kalemin hazır ya da servis edilmiş olduğunu görmediği için iptalin neden reddedilebileceğini önceden bilemiyordu. Sipariş okuma uç noktası kalem başına kitchenState alanını zaten döndürür; panel bu alanı okur ve Ready için "Hazır", Served için "Servis edildi" notunu kalem satırında gösterir. Yeni sunucu alanı veya uç nokta yoktur. Kaynak: kasiyer denemesi 6, sorun 2.

## Owned surface

- `evidence/V1-RMD-1044/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/contracts.ts
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1043

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

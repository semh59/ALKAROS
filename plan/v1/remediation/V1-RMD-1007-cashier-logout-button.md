# V1-RMD-1007 - Kasa ekranında Diğer menüsündeki Çıkış düğmesi çalışır

- Task ID: V1-RMD-1007
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasa satış ekranındaki Diğer menüsünde Çıkış (ve Müşteri ekranını eşleştir, Rezervasyon istasyonu) düğmesine basınca menü, işlem başlamadan kapanıp tıklamayı yutuyordu; oturum kapanmıyordu. Menü kapanması tıklamanın işlenmesinden sonra olur ve Çıkış oturumu kapatır.

## Owned surface

- `evidence/V1-RMD-1007/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.logout-clears-storage.test.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.header.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1006

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

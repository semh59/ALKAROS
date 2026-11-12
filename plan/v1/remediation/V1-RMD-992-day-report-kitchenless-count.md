# V1-RMD-992 - Gün sonu raporunda mutfağa gitmeden eklenen kalem sayısı ayrı görünür

- Task ID: V1-RMD-992
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Gün sonu ön kontrolünde, kapanmış gün raporunda ve yönetim kapanış ekranında mutfağa gitmeden hesaba eklenen ürün sayısı ayrı bir satır olarak gösterilir; sayı denetim kayıtlarından gün penceresine göre sayılır.

## Owned surface

- `evidence/V1-RMD-992/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Reporting/KitchenlessAddedCounter.cs
  - src/Host/Experience/Reporting/EndOfDayEndpoints.cs
  - tests/Host/Experience/Reporting/EndOfDayHttpTests.cs
  - src/Clients/PosTerminal/src/features/management-closing/models.ts
  - src/Clients/PosTerminal/src/features/management-closing/ClosingSection.tsx
  - src/Clients/PosTerminal/src/features/management-closing/ClosingSection.test.tsx
  - src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-991

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

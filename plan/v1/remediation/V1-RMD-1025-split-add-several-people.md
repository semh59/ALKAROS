# V1-RMD-1025 - Hesap bölmede Kişi ekle sayıyla birden çok kişi ekler

- Task ID: V1-RMD-1025
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Hesap bölme ekranında Kişi ekle her basışta tek kişi ekliyordu; 5 kişilik masa için 5 kez basmak gerekiyordu. Düğmenin yanına Kişi sayısı alanı eklenir (1 ile 20 arası, varsayılan 1); Kişi ekle seçilen sayıda kişiyi tek dokunuşla ekler.

## Owned surface

- `evidence/V1-RMD-1025/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.tsx
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.test.tsx
  - src/Clients/PosTerminal/src/features/billing/billing.css
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1024

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

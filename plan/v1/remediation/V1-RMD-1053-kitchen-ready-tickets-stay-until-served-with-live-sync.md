# V1-RMD-1053 - Canlı eşitleme açıkken Hazır biletler servis edilene kadar mutfak panosunda ayrı bölümde görünür

- Task ID: V1-RMD-1053
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Mutfak panosu tüm kalemleri Hazır olan bileti bir sonraki yoklamada panodan kaldırıyor (bump); bu yüzden Servis edildi adımına ulaşılamıyor ve kalem durumu siparişe Servis edildi olarak yansımıyor (7. kasiyer denemesi). Canlı eşitleme (kitchen.live_sync_enabled) açıkken, Hazır olup henüz tüm kalemleri servis edilmemiş biletler pano listesine de gelir ve istemcide "Hazır - servis bekliyor" ayrı bölümünde gösterilir; kalemlerde Servis edildi yapılabilir, hepsi servis edilince bilet panodan kalkar. Canlı eşitleme kapalıyken mevcut davranış aynen kalır (Hazır biletler panodan kalkar). Semih kararı 2026-11-14.

## Owned surface

- `evidence/V1-RMD-1053/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Kitchen/TicketLifecycle/IKitchenTicketRepository.cs
  - src/Modules/Kitchen/TicketLifecycle/PostgresKitchenTicketRepository.cs
  - src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs
  - tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
  - src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.tsx
  - src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build)

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1052

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

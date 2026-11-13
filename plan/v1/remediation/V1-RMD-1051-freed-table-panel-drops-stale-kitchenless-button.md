# V1-RMD-1051 - Boşalan masanın panelinde önceki siparişin "Mutfağa göndermeden hesaba ekle" düğmesi kalmaz

- Task ID: V1-RMD-1051
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Masa taşıma veya hesap kapanışıyla boşalan masanın panelinde, Yenile'ye basılana kadar önceki siparişin "Mutfağa göndermeden hesaba ekle" (ve gönderilmemiş kalem varsa "Mutfağa gönder") düğmesi kalıyordu (7. kasiyer denemesi, a notu). Kök neden: masa paneli sipariş kalemlerini sipariş kimliği yokken temizlemiyor, eski sipariş kalemleri düğme koşullarını beslemeye devam ediyor. Sipariş kimliği olmayan masada kalemler yok sayılır; boş masa yalnız "Sipariş al" ve ilgili düğmeleri gösterir. Semih kararı 2026-11-14.

## Owned surface

- `evidence/V1-RMD-1051/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build)

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1050

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

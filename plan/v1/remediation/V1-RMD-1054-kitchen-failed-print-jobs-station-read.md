# V1-RMD-1054 - Mutfak panosu başarısız yazdırma işlerini bilet başına değil tek istekle okur

- Task ID: V1-RMD-1054
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Planned

## Goal

Mutfak panosu her 8 saniyelik yoklamada açık her bilet için ayrı yazdırma işi isteği (GET print-jobs?ticketId=) atıyor; çalışan yığında 12 saatte 1591 istek görüldü (7. kasiyer denemesi yan bulgusu). Kaynak kodunda bu dağılımın nedeni açıkça not edilmiş: istasyon bazlı başarısız iş okuması yok (V1-RMD-293). Bu görev, istasyonun başarısız ve ölü yazdırma işlerini tek bir salt-okunur sunucu okumasıyla döndürür ve panoyu bilet başına isteklerden tek isteğe geçirir. Bu görev yalnız planlanmıştır; kod bu turda yazılmaz. Semih kararı 2026-11-14.

## Owned surface

- `evidence/V1-RMD-1054/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/KitchenOperations/KitchenOperationsEndpoints.cs
  - src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs
  - src/Host/Experience/KitchenOperations/KitchenOperationsContracts.cs
  - tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
  - src/Clients/PosTerminal/src/features/kitchen-operations/kitchenApi.ts
  - src/Clients/PosTerminal/src/features/kitchen-operations/kitchenApi.test.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build)

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1053

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

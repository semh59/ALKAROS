# V1-RMD-967 - Stok kaleminin varsayılan konumu oluşturulduktan sonra değiştirilebilsin

- Task ID: V1-RMD-967
- Status: Done
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi raporu: stok kalemi oluşturulurken seçilen varsayılan konum sonradan değiştirilemiyor (düzenle eylemlerinde yok; yanlış seçilen konum için kalemi silip yeniden açmak gerekiyor). Karar: yeniden sipariş noktası ucunun benzeri bir PUT stock-items/{id}/default-location ucu eklenir (konum boş bırakılırsa kaldırılır; var olan ve etkin konum gerekir, yoksa 404, etkin değilse mevcut 'pasif konum' hatası), stok ekranına 'Konumu değiştir' eylemi eklenir.

## Owned surface

- `evidence/V1-RMD-967/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Inventory/StockMaster/IStockMasterService.cs
  - src/Modules/Inventory/StockMaster/StockMasterService.cs
  - src/Host/Experience/Inventory/StockMasterContracts.cs
  - src/Host/Experience/Inventory/StockMasterEndpoints.cs
  - tests/Host/Experience/Inventory/StockMasterHttpTests.cs
  - tests/Host/Experience/Inventory/StockMasterTestDatabase.cs
  - tests/Modules/Inventory/StockMaster/StockMasterDatabaseTests.cs
  - tests/Modules/Inventory/StockMaster/StockMasterDomainTests.cs
  - tests/Architecture/ApiConventions/idempotency_allowlist.json
  - src/Clients/PosTerminal/src/features/management-stock/stockApi.ts
  - src/Clients/PosTerminal/src/features/management-stock/StockSection.tsx
  - src/Clients/PosTerminal/src/features/management-stock/StockSection.test.tsx
  - src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-966

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

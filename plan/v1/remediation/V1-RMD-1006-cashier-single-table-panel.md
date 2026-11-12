# V1-RMD-1006 - Masa planında tek sağ panel: masa, kalemler, ana eylem, mutfak ve ikincil eylemler

- Task ID: V1-RMD-1006
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Seçili masa için tek kutu: masa adı, durum, kalemler, toplam; boş masada Sipariş al, dolu masada Öde ana düğme; Ürün ekle, Mutfağa gönder (n) (gönderilmemiş kalem sayısı), Mutfağa göndermeden hesaba ekle; Böl, Masa taşı, İptal / iade. Masayı aç, Rezervasyon al, Masaları birleştir, Servis dışı yap gibi yönetici eylemleri paneldeki Diğer işlemler menüsüne girer. Metinler Hesabı al yerine Öde der.

## Owned surface

- `evidence/V1-RMD-1006/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.table-cart.test.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.kitchenless.test.tsx
  - src/Clients/PosTerminal/src/routes/ReservationStation.test.tsx
  - src/Clients/PosTerminal/src/tableCart.ts
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.tsx
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.test.tsx
  - src/Clients/PosTerminal/src/features/tables/tables.css
  - src/Clients/PosTerminal/src/features/tables/models.ts
  - src/Clients/PosTerminal/src/features/tables/tablesStyle.test.ts
  - src/Clients/PosTerminal/src/styles.css
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/api.ts
  - src/Clients/PosTerminal/src/api.test.ts
  - src/Clients/PosTerminal/src/contracts.ts
  - src/Clients/PosTerminal/src/routes/Cashier.ticket-footer.test.tsx
  - src/Host/DualScreen/DualScreenContracts.cs
  - src/Host/DualScreen/DualScreenStore.Display.cs
  - tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1005

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

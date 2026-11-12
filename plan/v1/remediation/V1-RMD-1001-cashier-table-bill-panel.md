# V1-RMD-1001 - Masa planının sağ paneli masanın hesabını ve eylemlerini taşır

- Task ID: V1-RMD-1001
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Seçili masanın hesabı (kalemler ve toplam) masa planının sağ panelinde görünür: boş masada Sipariş al, dolu masada Ürün ekle; Öde tek dolu düğmedir; Böl, Masa taşı, İptal/iade ikincil eylemlerdir. Hesabı açık masada ürün ekleme kapalıdır ve nedeni söylenir. Sepet panelinde Mutfağa gönder (n) düğmesi gönderilecek ürün sayısını taşır; Mutfağa göndermeden hesaba ekle düğmesi ve Tahsil et sepetin sabit alt bölümünde durur, kalem listesi uzasa da yarım gizli kalmaz.

## Owned surface

- `evidence/V1-RMD-1001/**`
- `src/Clients/PosTerminal/src/routes/Cashier.ticket-footer.test.tsx`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
  - src/Clients/PosTerminal/src/routes/ReservationStation.test.tsx
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.collect.test.tsx
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.tsx
  - src/Clients/PosTerminal/src/features/tables/TableWorkspace.test.tsx
  - src/Clients/PosTerminal/src/features/tables/FloorPlanWorkspace.tsx
  - src/Clients/PosTerminal/src/features/tables/FloorPlanWorkspace.test.tsx
  - src/Clients/PosTerminal/src/features/tables/models.ts
  - src/Clients/PosTerminal/src/features/tables/tables.css
  - src/Clients/PosTerminal/src/api.ts
  - src/Clients/PosTerminal/src/api.test.ts
  - src/Clients/PosTerminal/src/contracts.ts
  - src/Clients/PosTerminal/src/styles.css
  - src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Sağ panel: masa adı, kalemler, toplam; Sipariş al / Ürün ekle, Öde, Böl, Masa taşı, İptal/iade; her eylem mevcut istemci işlevine bağlı (api.startTableOrder, collectionHrefForOrder, Transfer, Hesap bölme ekranı); hesabı açık masada Ürün ekle kapalı ve neden yazılı (kırmızı test, mutasyon)
2. Üçüncü deneme #6: sepette Mutfağa gönder (n) ve mutfaksız ekle düğmesi sabit alt bölümde; kalem listesi kayar, düğmeler kırpılmaz
3. Üçüncü deneme #4: sağ panelde ve sepet başlığında masa adı görünür
4. Kat planı çiziminde yeni durum simgeleri

## Out of scope

- Listede olmayan ekranlar ve dosyalar
- Ödeme ekranı (1002), koyu tema

## Dependencies

- V1-RMD-1000

## Acceptance evidence

1. Kırmızı test, mutant listesi, oxlint --max-warnings=0 + tsc + vite build çıktıları (istemci değişiyorsa)
2. İki ardışık bağımsız qa-* incelemesi (qa-frontend ve qa-ux-tasarim) ve doğrulama turu.

## Handoff

- None

# V1-RMD-1194 - Satış ekranı yenilenince masa bağlamı korunur

- Task ID: V1-RMD-1194
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 18 (bulgu 2): masadan "Sipariş al" ile açılan satış ekranı yenilenince (F5) "HIZLI SİPARİŞ"e döner; masaya eklenmiş ama gönderilmemiş kalem masada taslak kalır ("Dolu"), kasiyer fark etmez. Kök neden: masa sepeti işareti (`tableCart.ts`, sessionStorage) açılışta okunurken silinir (`takeTableCart`); yenilemeden sonra işaret yoktur ve `openCheckOnly` masa siparişini sepet saymaz.

1. İşaret okunurken silinmez; satış ekranı yenilenince aynı masa siparişi sepet olarak geri yüklenir. İşaret yalnız kasiyer masa sepetinden bilerek çıkınca (Hızlı satış bağlantısı), satış ekranından başka sayfaya geçince, çıkış yapınca veya sipariş bitince (ödendi/iptal) silinir; Masalar ekranından yeni "Sipariş al" işareti değiştirir.
2. "Mutfaksız ekle" niyeti (`takeWithoutKitchenIntent`) bir kerelik kalır (yenilemede neden sorusu yeniden açılmaz).
3. Gönderilmemiş kalemi olan masa taslağı yenilemeden sonra görünür kalır; sessiz taslak yetimi oluşmaz.

## Owned surface

- `evidence/V1-RMD-1194/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/tableCart.ts
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.table-cart.test.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.logout-clears-storage.test.tsx
  - src/Clients/PosTerminal/src/storage.ts
  - docs/CASHIER_UI_RULES.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (masa sepetiyle açılıp yenileme: masa başlığı ve kalemler geri gelir; Hızlı satışa geçip yenileme: hızlı sipariş; başka sayfadan dönüş; çıkış temizler), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. PosTerminal ilgili vitest dosyaları ve istemci üretim derlemesi

## Out of scope

- Masalar ekranı ve masa planı rozetleri; sunucu taslak ömrü; workspace.tsx

## Dependencies

- V1-RMD-1193

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

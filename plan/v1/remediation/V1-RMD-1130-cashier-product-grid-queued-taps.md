# V1-RMD-1130 - Kasa ürün ızgarası ardışık dokunuşta kilitlenmez: dokunuşlar sıraya alınır

- Task ID: V1-RMD-1130
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Planned

## Goal

Kasiyer deneyimi turu 17: PosTerminal kasa ekranında bir ürüne dokunulunca sunucu yanıtı gelene kadar tüm ürün ızgarası kilitleniyor (disabled); hızlı ardışık dokunuşlar sessizce kayboluyor. Seçenek grubu olmayan ürünlere, kalem ekleme sürerken yapılan dokunuşlar ürün başına adet olarak sıraya alınır; ilk ekleme bitince sıradaki ürünler tek tek, her biri bir öncekinin döndürdüğü güncel revizyonla (ikinci tur taze sipariş okunarak) gönderilir; ızgara bu sırada kilitlenmez. Çift ekleme olmaz: her dokunuş tam bir kez gönderilir, ağ/5xx hatasında kalan sıra düşürülür ve yeniden denenmez (kalem ekleme işlem anahtarı taşımaz), 409'da sıra düşürülür ve sipariş yeniden okunur. Seçenek penceresi gerektiren ürünler ve mutfaksız satış kilitli kalmaya devam eder; başka işlem sürerken (gönder, kaldır, adet) ızgara eskisi gibi kilitlidir. Sunucu sözleşmesi ve revizyon kuralı değişmez.

## Owned surface

- `evidence/V1-RMD-1130/**`
- `src/Clients/PosTerminal/src/routes/Cashier.rapid-taps.test.tsx` (yeni)
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.first-touch.test.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.modifier-picker.test.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.search-add.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci typecheck, testler ve üretim derlemesi (client-build-gate)

## Out of scope

- Sunucu sözleşmesi; seçenekli ürün ve mutfaksız satış akışları; bekletme (park) ve kalem notu (sunucu sözleşmesi yok); fiziksel çekmece

## Dependencies

- V1-RMD-1127

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

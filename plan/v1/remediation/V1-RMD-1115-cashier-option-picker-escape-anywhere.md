# V1-RMD-1115 - Seçenek penceresi Escape ile her durumda kapanır

- Task ID: V1-RMD-1115
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu 13: seçenekli ürünün (örn. Kahve) seçenek penceresi açıkken Escape, odak pencere içinde bir yazıya veya boşluğa tıklanarak pencere dışına (body) düştüğünde pencereyi kapatmıyor; yalnız Vazgeç kapatıyor. Pencere, odak nerede olursa olsun Escape ile kapanır ve odak ürüne döner; pencere dışına tıklama mevcut modal kalıbıyla pencereyi kapatmaya devam eder (arka plandaki ürünler modal sırasında tıklanamaz, bu kasıtlıdır). Odak tuzağı, aria-modal ve 48 px dokunma kuralları bozulmaz.

## Owned surface

- `evidence/V1-RMD-1115/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/CashierModifierPicker.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.modifier-picker.test.tsx
  - src/Clients/PosTerminal/src/routes/cashier-modifier-picker.css
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Sepet ve sipariş kuralları; eşleştirme penceresi

## Dependencies

- V1-RMD-1114

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

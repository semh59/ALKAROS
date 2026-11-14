# V1-RMD-1070 - Boş siparişte sağ panelde toplam bölümü ve pasif Mutfağa gönder düğmesi çizilmez

- Task ID: V1-RMD-1070
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Satış ekranında sipariş boşken sağ panelde Ara toplam, KDV, Toplam ₺0,00 ve nedensiz pasif "Mutfağa gönder (0)" çiziliyor (kasiyer denemesi 8; docs/CASHIER_UI_RULES.md 3 nedensiz pasif düğme yoktur). Satır yokken toplam bölümü ve gönder düğmesi çizilmez; panelde "Soldaki katalogdan ürün seçin." yönlendirmesi kalır.

## Owned surface

- `evidence/V1-RMD-1070/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.ticket-footer.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build)

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-1069

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

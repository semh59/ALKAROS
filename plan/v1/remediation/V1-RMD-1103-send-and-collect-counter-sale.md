# V1-RMD-1103 - Tezgâh satışında tek düğmeyle gönder ve tahsil et

- Task ID: V1-RMD-1103
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer deneyimi turu: masasız (hızlı) satışta kasiyer önce "Mutfağa gönder", sonra "Tahsil et" düğmesini arıyor. Düzenlenebilir, kalemli, masasız siparişte ve kullanıcıda hesap bölme yetkisi (bills.split) varken "Gönder ve tahsil et" düğmesi siparişi gönderir ve gönderim başarılıysa doğrudan ödeme sayfasına geçer; gönderim hatalıysa ödeme sayfasına geçilmez. Mevcut "Mutfağa gönder", "Tahsil et", mutfaksız gerekçe akışı ve nakit için kasa kuralı (ödeme sayfasında) değişmez. Masa siparişinde düğme görünmez.

## Owned surface

- `evidence/V1-RMD-1103/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/routes/Cashier.send-and-collect.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate)

## Out of scope

- Mutfaksız ekleme akışı; masa siparişi; sunucu sözleşmesi

## Dependencies

- V1-RMD-1102

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

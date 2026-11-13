# V1-RMD-1058 - Masa panelinde İptal / iade ayrı satırda yıkıcı stille durur, düğmeler arası boşluk 16 pikseldir ve pasif düğmenin nedeni yazılır

- Task ID: V1-RMD-1058
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Masa panelinde Böl, Masa taşı ve İptal / iade üç düğme yan yana 8-9 piksel boşlukla duruyor, 768 piksel genişlikte iki satıra kırılıyor ve yıkıcı İptal / iade diğerleriyle aynı stili taşıyor; Masa taşı pasifken nedeni yazılmıyor; panel şeftali zeminli, yazılar 13 piksel ve jeton dışı renkli. docs/CASHIER_UI_RULES.md'ye göre: İptal / iade kendi satırında ve yıkıcı stilde (kırmızı yazı ve kenar) durur, Böl ile Masa taşı yan yana kalır, düğmeler arası boşluk en az 16 piksel olur, masa taşınamıyorsa nedeni panelde yazılır, panel yazıları en az 14 piksel olur ve panel zemini ile uyarı renkleri jetonlardan gelir. İptal / iade'nin gittiği ödeme sayfasındaki onay düğmesi başka görevdedir (V1-RMD-1059).

## Owned surface

- `evidence/V1-RMD-1058/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
  - src/Clients/PosTerminal/src/styles.css
  - src/Clients/PosTerminal/src/strings.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build)

## Out of scope

- Listede olmayan ekranlar ve dosyalar; masa planı kartları (V1-RMD-1056); ödeme sayfası ve iade formu (V1-RMD-1057, V1-RMD-1059)

## Dependencies

- V1-RMD-1057

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

# V1-RMD-1036 - Masa panelinde Mutfağa gönder ve Masa taşı sonrası kartlar ve kalemler kendiliğinden güncellenir

- Task ID: V1-RMD-1036
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer masa panelinde "Mutfağa gönder" veya "Masa taşı" dedikten sonra ekran "Yenile"ye basmadan güncel durumu gösterir. Kök neden iki parçadır: panelin kalem listesi yalnız masa sürümü değişince yeniden okunuyordu (gönderim sürümü değiştirmediği için "Mutfağa gönderilmedi" kalıyordu) ve masa taşıma sonrası sipariş-masa bağı sunucuda olay kuyruğuyla (en çok ~1 sn) geç yazıldığı için hemen yapılan tek yenileme boşalan masada eski tutarı okuyordu. Masa eylemi ve gönderim sonrası tek yenileme yolu kullanılır: hemen bir okuma, olay kuyruğunun oturması için bir okuma daha; panel kalemleri gönderimden sonra yeniden okunur.

## Owned surface

- `evidence/V1-RMD-1036/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/workspace.tsx
  - src/Clients/PosTerminal/src/routes/workspace.test.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar; sunucu taşıma akışı

## Dependencies

- V1-RMD-1035

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

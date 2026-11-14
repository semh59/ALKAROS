# V1-RMD-1083 - Hesap istedi durumundaki masanın paneli Öde sunar

- Task ID: V1-RMD-1083
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasa kullanıcı denemesi 10: garson hesabı kasaya gönderdiğinde masa planında "Hesap istedi" görünür ama masanın paneli masada açık hesap yok diyor, "Öde" sunmuyor ve "sipariş almaya uygun değil" yazıyor. Bekleyen hesaplar listesindeki (awaiting-payment) sipariş numarası masa paneline iletilir; panel bu durumda "Öde" düğmesini ve yön veren bir cümleyi gösterir, yanlış "uygun değil" gerekçesini göstermez. docs/CASHIER_UI_RULES.md ve masa planı kuralları değişmez.

## Owned surface

- `evidence/V1-RMD-1083/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
  - src/Clients/PosTerminal/src/routes/workspace.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build)

## Out of scope

- Listede olmayan ekranlar ve dosyalar; arka uç sözleşmesi

## Dependencies

- V1-RMD-1082

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

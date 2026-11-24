# V1-RMD-1187 - Kalem notu masa detay panelinde ve ön hesap fişinde görünür

- Task ID: V1-RMD-1187
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 17 (bulgu 4): kalem notu satış panelinde görünür ama masa detay panelinde (`TableOrderBridge`; sunucu kalemi `specialInstructions` zaten döner, istemci tipi taşımıyor) ve ön hesap fişinde yoktur; kasiyer girdiği notu masadan doğrulayamaz.

1. Masa panelinde notlu kalemin altında "Not: ..." satırı (14 px) görünür.
2. Ön hesap fişinde `BillPreReceiptLineV1` isteğe bağlı `Note` alanı taşır (hesap açıkken fatura satırı sipariş kalemine `OrderItemId` ile bağlanır) ve fiş kalemin altına "Not: ..." yazar. Not en çok `DualScreenStore.MaxSpecialInstructionsLength` (200) karakterdir; sipariş tarafında sınır olmadığı için sunucu kırpar. V1-RMD-1177 yapısı (başlık, ibare, HESAP NO, Tarih, Saat, toplamlar) değişmez.
3. Not serbest metindir (KVKK); pilot belgesindeki "kişisel bilgi yazma" uyarısı korunur. `docs/domain/bill-pre-receipt.md` güncellenir.

## Owned surface

- `evidence/V1-RMD-1187/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenApplication.Receipt.cs
  - tests/Host/Experience/PaymentTender/BillReceiptHttpTests.cs
  - src/Clients/PosTerminal/src/contracts.ts
  - src/Clients/PosTerminal/src/features/pending-checks/preReceipt.ts
  - src/Clients/PosTerminal/src/features/pending-checks/preReceipt.test.ts
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.tsx
  - src/Clients/PosTerminal/src/routes/TableOrderBridge.test.tsx
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/styles.css
  - docs/domain/bill-pre-receipt.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (sunucu: ön fiş satırı notu, hesap yokken ve hesap varken, 200 sınırı; istemci: fişte ve masa panelinde not), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. PaymentTender ilgili HTTP testleri, ApiConventions, PosTerminal ilgili vitest dosyaları ve istemci üretim derlemesi

## Out of scope

- Ödenmiş hesap fişi; mutfak fişi; not sınırının sipariş tarafında değiştirilmesi; seçenek (modifier) ayrıntısı

## Dependencies

- V1-RMD-1186

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

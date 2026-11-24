# V1-RMD-1189 - Mutfak panosunda kalem seçenekleri her yoğunluk modunda görünür

- Task ID: V1-RMD-1189
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-1184 yoğun modda kalem notunu görünür yaptı ama seçenek metni (`.kitchen-item-row__detail`, örn. "Ekstra peynir", "Az acılı", porsiyon) yoğun modda hâlâ `display: none`. Seçenek siparişin içeriğidir; gizlenirse aşçı yanlış yemek çıkarır (not ile aynı risk sınıfı). Pano 18 açık kalemde kendiliğinden yoğun moda girdiği için bu gerçek bir yolda olur. Düzeltme: yoğun mod seçenek ayrıntısını gizlemez; ürün adı, adet, seçenek, not ve rozetler her modda görünür, kısaltılmaz, 14 px (0.875rem) alt sınırı korunur. Mutfak fişi (`EscPosTicketFormatter`) seçenek (`+ ...`) ve notu (`NOT: ...`) zaten basıyor; fişte değişiklik gerekmez. `docs/CASHIER_UI_RULES.md` mutfak kuralı seçeneği de kapsayacak şekilde güncellenir.

## Owned surface

- `evidence/V1-RMD-1189/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/kitchen-operations/kitchen-operations.css
  - src/Clients/PosTerminal/src/features/kitchen-operations/kitchenDenseNote.test.ts
  - docs/CASHIER_UI_RULES.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (yoğun modda ürün adı, seçenek ve not gizleme kurallarıyla gizlenmez; sakin modda da), düzeltme (yoğun mod gizleme kuralının kaldırılması), mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate) ve mutfak ilgili vitest dosyaları

## Out of scope

- Pano düzeni, renkler, sunucu, fiş biçimi, eski istemciler

## Dependencies

- V1-RMD-1184

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

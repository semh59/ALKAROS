# V1-RMD-1193 - Mutfak fişi kalemi seçenekleri ayrı dizi olarak taşır

- Task ID: V1-RMD-1193
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-1190/1191 açık riski: sunucu seçenekleri tek dizede birleştirir (`modifiersSummary`, `KitchenTicket.cs`) ve mutfak istemcisi `, ` ile bölür; seçenek adında virgül varsa ("Peynir, çift") yanlış bölünür ve Tüm Gün gruplaması bozulur.

1. Sunucu sözleşmesi geriye uyumlu genişler: `KitchenTicketItemV1` isteğe bağlı `ModifierNames` (dize dizisi) taşır; her öğe, eski `Modifiers` dizesinin birleştirildiği parçanın aynısıdır (adet 1'den büyükse "2× ad"). Eski `Modifiers` alanı aynen kalır. Değerler `orders.order_item_modifiers` kayıtlarından okunur (kalem `OrderItemId` ile bağlı); şema ve migration değişmez. Bilet okuyan her uç (pano listesi, tek bilet, durum değişimleri) aynı alanı döner; seçeneği olmayan kalemde alan yazılmaz.
2. Mutfak istemcisi (`models.ts`, `KitchenOperationsWorkspace.tsx`) alan varsa onu kullanır, yoksa eski dizeyi bölmeye döner; kalem satırı ve Tüm Gün gruplaması virgüllü adı tek seçenek sayar.

## Owned surface

- `evidence/V1-RMD-1193/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/KitchenOperations/KitchenOperationsContracts.cs
  - src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs
  - tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
  - src/Clients/PosTerminal/src/features/kitchen-operations/models.ts
  - src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.tsx
  - src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.test.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (sunucu: virgüllü ad tek öğe, adetli ad, seçeneksiz kalemde alan yok, eski alan aynı; istemci: virgüllü seçenek tek seçenek, alan yokken eski dize), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. KitchenOperations HTTP testleri, ApiConventions ve allowlist testleri, mutfak vitest dosyaları ve istemci üretim derlemesi

## Out of scope

- Fiş yazıcısı biçimi (`EscPosTicketFormatter`); `modifiers_summary` sütunu ve migration; mutfak pano düzeni; kasiyer ekranları (V1-RMD-1192)

## Dependencies

- V1-RMD-1192

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

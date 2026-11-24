# V1-RMD-1190 - Mutfak "Tüm Gün" özeti ürünü seçenek ve nota göre ayırır

- Task ID: V1-RMD-1190
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Mutfak panosundaki "Tüm Gün" görünümü açık kalemleri yalnız ürüne göre toplar ve seçenek ile notu göstermez. Görünüm, aşçının toplu hazırlık yapması için kullanılır (V1-KDS-008, Toast All Day View: "x adet köfte hazırla"). "Soğansız köfte" normal köfte ile tek satırda birleşirse aşçı toplu hazırlıkta yanlış yemek çıkarır. V1-KDS-008 modifier alt gruplamasını bilinçli olarak "fast-follow" bıraktı; kalıcı bir tasarım kararı değil. Düzeltme: özet ürün + seçenekler + not birleşimine göre gruplanır; aynı ürün, aynı seçenekler ve aynı not tek satırda toplanır, farklıysa ayrı satır olur ve her satırda adet yazar. Seçenekler deterministik sıralanır (aynı seçenekler farklı sırada gelirse aynı grup). Seçenek ve not satırda görünür, 14 px (0.875rem) alt sınırı korunur.

## Owned surface

- `evidence/V1-RMD-1190/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.tsx
  - src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.test.tsx
  - src/Clients/PosTerminal/src/features/kitchen-operations/kitchen-operations.css
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (iki köfte biri soğansız iki satır; aynı seçenekler farklı sırada tek satır; aynı not toplanır), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate) ve mutfak ilgili vitest dosyaları

## Out of scope

- Pano kart düzeni, sunucu, fiş biçimi, Held kalem mantığı

## Dependencies

- V1-KDS-008
- V1-RMD-1189

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

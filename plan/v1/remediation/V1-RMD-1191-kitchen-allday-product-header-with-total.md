# V1-RMD-1191 - Mutfak "Tüm Gün" özeti ürün başlığında toplam adedi gösterir

- Task ID: V1-RMD-1191
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

V1-RMD-1190 "Tüm Gün" özetini ürün + seçenekler + not kombinasyonuna göre ayırdı; yan etkisi: aynı ürünün toplam adedi hiçbir yerde görünmüyor ve aynı ürünün kombinasyon satırları adede göre dizildiği için birbirinden uzağa düşebiliyor. Düzeltme: özet ürün bazında gruplanır. Birden fazla kombinasyonu olan ürün için bir başlık satırı (ürün adı + toplam adet, büyük) ve altında kombinasyon satırları (seçenekler + not + adet, 14 px alt sınır) gösterilir. Yalnız bir kombinasyonu olan ürün tek satırda ürün + adet + seçenek + not gösterir (gereksiz iç içelik yok). Ürünler toplam adede göre azalan, kombinasyonlar adede göre azalan sıralanır; eşitlikte ad sırası (tr) belirleyicidir. Sunucu seçenekleri tek dize (`modifiersSummary`) gönderdiği için seçenek adındaki virgül istemcide bölünür; bu görev sunucu sözleşmesini değiştirmez, risk RESULT'a yazılır.

## Owned surface

- `evidence/V1-RMD-1191/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.tsx
  - src/Clients/PosTerminal/src/features/kitchen-operations/KitchenOperationsWorkspace.test.tsx
  - src/Clients/PosTerminal/src/features/kitchen-operations/kitchen-operations.css
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (aynı ürünün 3 kombinasyonu: başlıkta toplam, altında 3 satır; tek kombinasyon tek satır; deterministik sıralama), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate) ve mutfak ilgili vitest dosyaları

## Out of scope

- Sunucu sözleşmesi (seçeneklerin ayrı dizi olması), pano kart düzeni, fiş biçimi, Held kalem mantığı
- docs ve V1-KDS-008 belge notları (sahibi başka görev)

## Dependencies

- V1-RMD-1190

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

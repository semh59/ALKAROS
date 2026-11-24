# V1-RMD-1184 - Mutfak panosunda kalem notu her yoğunluk modunda görünür

- Task ID: V1-RMD-1184
- Status: Done
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi 17 (bulgu 1, kritik): `kitchen-operations.css` yoğun modda (`.kitchen-workspace.is-dense .kitchen-item-row__detail { display: none }`) hem seçenek metnini hem kalem notunu (`.kitchen-item-row__detail--note`, "soğansız", "az pişmiş") gizliyor. Pano açık kalem sayısı eşiği geçince kendiliğinden yoğun moda girdiği için aşçı notu görmüyor; not yemek güvenliği ve alerji verisidir. Yoğun mod seçenek ayrıntısını gizlemeye devam eder; notlu kalemin notu her modda (sakin, yoğun, otomatik) görünür kalır, kısaltılmaz, uyarı rengi ve kalın yazıyla vurgulu, 14 px altına inmez. `docs/CASHIER_UI_RULES.md` mutfak panosu kuralına tek cümle eklenir.

## Owned surface

- `evidence/V1-RMD-1184/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/kitchen-operations/kitchen-operations.css
  - src/Clients/PosTerminal/src/features/kitchen-operations/kitchenDenseNote.test.ts
  - docs/CASHIER_UI_RULES.md
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test (gerçek CSS dosyası bir DOM'a yüklenir; yoğun mod + notlu kalem + seçenekli kalem: not hesaplanmış `display` değeri `none` değil, seçenek ayrıntısı yoğunda gizli, sakinde görünür), düzeltme, mutasyon (`cp` + `cmp`)
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (client-build-gate) ve mutfak ilgili vitest dosyaları

## Out of scope

- Seçenek (modifier) ayrıntısının yoğun modda gizli kalması (ayrı karar; riski RESULT'a yazılır); pano düzeni, renkler, sunucu

## Dependencies

- V1-RMD-1183

## Acceptance evidence

1. Kırmızı, yeşil ve mutant çıktıları; sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

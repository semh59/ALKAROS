# V1-RMD-1086 - Mutfak panosunda 14 pikselden küçük yazı kalmaz

- Task ID: V1-RMD-1086
- Status: InProgress
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

Semih kararı: mutfak panosundaki 14px altı yazılar 14px'e çıkar. Ölçülen kaynaklar: "hedef 15 dk" 9,9px, istasyon etiketi, geri al düğmesi, yazıcı ve yedek ikincil metinleri 11,5px, adım düğmeleri (Bekliyor, Hazırlanıyor, Hazır, Servis edildi) en çok 12,5px. kitchen-operations.css içindeki 14px altı TÜM yazı boyutları 14px'e çıkarılır. Dört adım düğmesi 14px'te dar genişlikte sarabilir; düğmeler esnek genişlikte ve metin tek satırda kalır, 48px dokunma hedefi korunur. docs/CASHIER_UI_RULES.md 2 punto kuralı mutfak panosunu da kapsar; yeni bir test mutfak CSS'inde 14px altı yazı kalmadığını ve belgenin bu kapsamı söylediğini korur.

## Owned surface

- `evidence/V1-RMD-1086/**`
- `src/Clients/PosTerminal/src/features/kitchen-operations/kitchenTextSize.test.ts`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/features/kitchen-operations/kitchen-operations.css
  - docs/CASHIER_UI_RULES.md
  - src/Clients/PosTerminal/src/design-system/cashierUiRules.test.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build)

## Out of scope

- Mutfak panosu dışındaki ekranlar; yazı dışı boyutlar; gerçek cihazda ölçüm

## Dependencies

- V1-RMD-1085

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

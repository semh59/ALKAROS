# V1-RMD-1065 - Kasiyer ekranlarında üst başlık, menü, satış ve form yazıları 14 piksele çıkarılır

- Task ID: V1-RMD-1065
- Status: Planned
- Assignee: claude-code-session_019vXeYZLNSUCBpqVmhFZUQZ
- Work type: implementation
- Surface state: Existing

## Goal

docs/CASHIER_UI_RULES.md 2 en küçük yazıyı 14px ister; kasiyer denemesi 8 üst başlık ve menüde 10,9-13,6px, satış ekranında yaklaşık 35 öğede 12px ve masa panelindeki "Kasada eklendi" notunda 11,7px ölçtü. shell.css, styles.css (müşteri ekranı seçicileri hariç), primitives.css ve billing.css içindeki 14px altı yazı boyutları 14px'e çıkarılır; <small> öğeleri açık boyut taşımadığında tarayıcı varsayılanıyla küçülmesin diye 14px alır; yeni bir test bu dosyalarda 14px altı yazı kalmadığını (yalnız adı konmuş müşteri ekranı seçicileri hariç) korur. Salon planı tuvali ayrı görevdedir (V1-RMD-1066).

## Owned surface

- `evidence/V1-RMD-1065/**`
- `src/Clients/PosTerminal/src/cashierTextSize.test.ts`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/shell/shell.css
  - src/Clients/PosTerminal/src/styles.css
  - src/Clients/PosTerminal/src/design-system/primitives.css
  - src/Clients/PosTerminal/src/features/billing/billing.css
  - src/Clients/PosTerminal/src/shell/shellStyle.test.ts
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi
3. İstemci üretim derlemesi (oxlint --max-warnings=0, tsc, vite build)

## Out of scope

- Listede olmayan ekranlar ve dosyalar; müşteri ekranı (display-*) ve yönetim ekranları; salon planı tuvali

## Dependencies

- V1-RMD-1064

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. client-build-gate çıktısı exit code 0
3. İki ardışık bağımsız qa-* incelemesi

## Handoff

- None

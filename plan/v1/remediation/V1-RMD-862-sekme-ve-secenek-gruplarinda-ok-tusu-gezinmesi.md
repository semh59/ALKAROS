# V1-RMD-862 - Sekme ve seçenek gruplarında ok tuşu gezinmesi (kategori sekmeleri, ödeme yöntemi, müşteri seçimi, dört React sekme grubu)

- Task ID: V1-RMD-862
- Status: Done
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

G9 (F0915): role=tablist ve role=radiogroup gruplarında yalnız Tab ile gezilebiliyor, ok tuşu çalışmıyor (ArrowLeft/ArrowRight yalnız FloorPlanWorkspace içinde var). Çözüm: Cashier kategori sekmeleri (cashier-app.js), split-payment ödeme yöntemi ve müşteri seçimi (split-payment.js) ve PosTerminal dört sekme grubu (Cashier.tsx kategori rayı, BillSplitWorkspace dağıtım modu, OnlineFoodHub sekmeleri, UserJourneysSection görünümleri) için sağ/aşağı sonraki, sol/yukarı önceki seçeneğe odak verip seçer (başa ve sona sarar). Devre dışı seçenekler atlanır. cash-session.js içinde sekme/radyo grubu yoktur; BusinessIdentitySettings yerel radyo girdileri kullandığı için tarayıcı zaten ok tuşunu işler. Cashier kategori sekmesi tıklamada odağı kaybediyordu; seçim sonrası odak korunur.

## Owned surface

- `evidence/V1-RMD-862/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/Cashier/wwwroot/cashier-app.js — kategori sekmeleri ok tuşu ve odak korunumu.
  - src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js — yöntem ve müşteri grubu ok tuşu.
  - src/Clients/PosTerminal/src/design-system/primitives.tsx — paylaşılan ok tuşu yardımcısı.
  - src/Clients/PosTerminal/src/routes/Cashier.tsx — kategori rayı.
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.tsx — dağıtım modu sekmeleri.
  - src/Clients/PosTerminal/src/features/online-hub/OnlineFoodHub.tsx — online sekmeler.
  - src/Clients/PosTerminal/src/features/management-user-journeys/UserJourneysSection.tsx — görünüm sekmeleri.
  - tests/Clients/StaticApps/arrow-key-groups.test.js — vanilla testler.
  - src/Clients/PosTerminal/src/design-system/primitives.test.tsx — yardımcı testi.
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.test.tsx — sekme bağlantısı testi.
  - src/Clients/PosTerminal/src/features/management-user-journeys/UserJourneysSection.test.tsx — sekme bağlantısı testi.
  - src/Clients/PosTerminal/src/features/online-hub/OnlineFoodHub.test.tsx — sekme bağlantısı testi.
  - src/Clients/PosTerminal/src/routes/Cashier.category-tabs.test.tsx — sekme bağlantısı testi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kullanıcıya görünen metin eklenmez.
2. Her grup için ok tuşu testi; mutasyonla (yön, sarma, devre dışı atlama) kırıldığı kanıtlanır.

## Out of scope

- Roving tabindex değişikliği; BusinessIdentitySettings yerel radyoları; FloorPlanWorkspace.

## Dependencies

- None

## Acceptance evidence

1. Vitest (StaticApps ve PosTerminal ilgili dosyalar) exit 0, tsc ve lint temiz.
2. Plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
3. İki ardışık bağımsız qa-* incelemesi.

## Handoff

- None

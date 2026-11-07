# V1-RMD-873 - Sekme listelerinde gezinen tabindex: yalnız seçili sekme Tab sırasında

- Task ID: V1-RMD-873
- Status: Planned
- Assignee: claude-code-session_01FMN5WTVUmS2xYXu1gL4Tsz
- Work type: implementation
- Surface state: Existing

## Goal

WAI-ARIA sekme desenine göre bir sekme listesi Tab sırasında tek durak olmalı (seçili sekme tabindex=0, diğerleri -1) ve sekmeler arasında ok tuşları gezinmelidir. Bugün dört React sekme listesinde (Cashier kategori rayı, BillSplitWorkspace dağıtım modu, UserJourneysSection görünüm, OnlineFoodHub bölümleri) ve vanilla Cashier kategori sekmelerinde (cashier-app.js) bütün sekmeler Tab sırasında; klavye kullanıcısı onlarca sekmeden tek tek geçer. Ok tuşu işleyicisi (moveBetweenTabs) zaten odaklanır ve seçer; yalnız tabindex eksik. Hiçbiri seçili değilse (vanilla kategori sekmeleri) ilk sekme Tab durağı kalır, böylece liste klavyeyle erişilemez olmaz.

## Owned surface

- `evidence/V1-RMD-873/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Clients/PosTerminal/src/routes/Cashier.tsx — kategori rayı sekmeleri.
  - src/Clients/PosTerminal/src/routes/Cashier.category-tabs.test.tsx — kategori rayı tabindex testi.
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.tsx — dağıtım modu sekmeleri.
  - src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.test.tsx — tabindex testi.
  - src/Clients/PosTerminal/src/features/management-user-journeys/UserJourneysSection.tsx — görünüm sekmeleri.
  - src/Clients/PosTerminal/src/features/management-user-journeys/UserJourneysSection.test.tsx — tabindex testi.
  - src/Clients/PosTerminal/src/features/online-hub/OnlineFoodHub.tsx — bölüm sekmeleri.
  - src/Clients/PosTerminal/src/features/online-hub/OnlineFoodHub.test.tsx — tabindex testi.
  - src/Clients/Cashier/wwwroot/cashier-app.js — vanilla kategori sekmeleri.
  - tests/Clients/StaticApps/arrow-key-groups.test.js — vanilla tabindex testi.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı testler: her listede tam bir sekme tabindex=0 (seçili olan), diğerleri -1; ok tuşuyla seçim değişince tabindex yeni seçiliye geçer; hiçbiri seçili değilse (vanilla) ilk sekme 0.
2. Mutasyon: her biri için seçili ölçütünün kaldırılması (hepsi 0) ve vanilla ilk-sekme yedeğinin kaldırılması (cp+cmp geri alınır).

## Out of scope

- Tablist kapsayıcısı tabindex=-1 deseninin değiştirilmesi; split-payment radiogroup (sekme değil).

## Dependencies

- None

## Acceptance evidence

1. PosTerminal vitest, tsc ve oxlint temiz; StaticApps vitest exit 0; çıktılar evidence/V1-RMD-873/.
2. Mutasyon kanıtı cp+cmp ile geri alınmış.
3. Plan audit ve consistency audit 0 hata/0 uyarı.
4. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

# V1-KDS-006 - Ayarlanabilir yoğun mod eşiğini ekrana bağlama

- Task ID: V1-KDS-006
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`V1-KIT-013`'ün açtığı `kitchen.dense_mode_threshold` değerini Mutfak
ekranına bağlar — `KitchenOperationsWorkspace.tsx`'teki sabit
`AUTO_DENSE_OPEN_ITEM_THRESHOLD = 9` kaldırılır, yerine `data`'dan gelen
değer kullanılır. Ayar hiç değiştirilmemişse davranış bugünküyle birebir
aynı kalır (varsayılan zaten `9`).

## Owned surface

- src/Clients/PosTerminal/src/features/kitchen-operations/** (Sınırlı ek
  — V1-KDS-001 sahipliğinde kalan dosyalar) — `AUTO_DENSE_OPEN_ITEM_THRESHOLD`
  sabitinin kaldırılıp `KitchenData.denseModeThreshold`'a taşınması,
  `kitchenApi.ts`'in `/operations/live-sync` yanıtından bu alanı okuması.
- src/Clients/PosTerminal/src/routes/workspace.tsx (Sınırlı ek, paylaşılan
  — V1-KDS-001 sahipliğinde kalan dosya) — `emptyKitchenData` fixture'ına
  `denseModeThreshold: 9` (yeni zorunlu alan, tip hatasını önlemek için).

## Out of scope

- `V1-KIT-013`'ün kendisi.
- Eşiği değiştirecek bir arayüz (yönetici ekranından `SetValueAsync`
  çağrısı) — bu görev yalnız OKUR, yazmaz.

## Dependencies

- V1-KIT-013

## Acceptance evidence

- `cd src/Clients/PosTerminal && npx tsc --noEmit` → **0 hata** (doğrulandı).
- `cd src/Clients/PosTerminal && npx vitest run` → **Test Files 23 passed
  (23), Tests 158 passed (158)** — tüm proje, izole değil (4 yeni test:
  `KitchenOperationsWorkspace.test.tsx`'te eşik `1`'e düşürülünce paylaşılan
  fixture'ın tek açık kalemiyle bile otomatik yoğun moda geçtiği VE eşik
  `5`'e çıkarılınca sakin modda kaldığı — ikisi de hardcoded `9`
  varsayımıyla YANLIŞ sonuç verirdi, gerçekten `data.denseModeThreshold`'u
  okuduğunu kanıtlıyor; `kitchenApi.test.ts`'te `denseModeThreshold: 15`
  uçtan gerçekten okunduğu, varsayılana düşülmediği — V1-KDS-004'ün kendi
  independent-review dersiyle aynı desen).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı (doğrulandı).
- `python tools/consistency-audit/consistency_audit.py` → `clean`
  (doğrulandı).
- Semih'in elle deneyebileceği senaryo: eşiği `2`'ye düşür, iki açık
  kalemle bile ekranın otomatik yoğun moda geçtiğini doğrula.

## Handoff

- None

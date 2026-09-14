# V1-KDS-006 - Ayarlanabilir yoğun mod eşiğini ekrana bağlama

- Task ID: V1-KDS-006
- Status: Planned
- Assignee: Unassigned
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

## Out of scope

- `V1-KIT-013`'ün kendisi.
- Eşiği değiştirecek bir arayüz (yönetici ekranından `SetValueAsync`
  çağrısı) — bu görev yalnız OKUR, yazmaz.

## Dependencies

- V1-KIT-013

## Acceptance evidence

- `cd src/Clients/PosTerminal && npx tsc --noEmit` → 0 hata.
- `cd src/Clients/PosTerminal && npx vitest run` → tüm proje yeşil (yeni
  test: backend'in döndürdüğü eşik değeri sabit `9` değilken de
  otomatik yoğun modun doğru tetiklendiği — hardcoded bir `9`
  varsayımıyla da geçecek bir testten kaçınılır, V1-KDS-004'ün kendi
  independent-review dersiyle aynı).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → `clean`.
- Semih'in elle deneyebileceği senaryo: eşiği `2`'ye düşür, iki açık
  kalemle bile ekranın otomatik yoğun moda geçtiğini doğrula.

## Handoff

- None

# V1-KIT-013 - kitchen.dense_mode_threshold değerini Kitchen HTTP yüzeyine aç

- Task ID: V1-KIT-013
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

`V1-SET-005`'in kaydettiği `kitchen.dense_mode_threshold` ayarını Kitchen
HTTP yüzeyinden okunabilir hale getirir — `V1-KIT-010`'un
`kitchen.live_sync_enabled` için yaptığının birebir aynısı, aynı uç
üzerine (`/operations/live-sync`) yeni bir alan olarak eklenir (ayrı bir
GET yerine — mevcut çağrılan bir uca eklemek zaten `V1-KIT-010`'un
kendi tercihiydi, gereksiz bir ağ isteği eklenmesin).

## Owned surface

- src/Host/Experience/KitchenOperations/KitchenOperationsContracts.cs,
  KitchenOperationsEndpoints.cs, KitchenOperationsStore.cs (Sınırlı ek —
  V1-RMD-082 sahipliğinde kalan dosyalar) — `LiveSyncStatusV1`'e
  `DenseModeThreshold: int` alanı (ya da yeni bir alan taşıyan aynı
  record'un genişletilmesi — uygulama sırasında karar verilir).
- tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
  (Sınırlı ek) — yeni test.

## Out of scope

- Frontend'in bunu kullanması (`V1-KDS-006`).
- Ayarı değiştirme (yalnız okuma).

## Dependencies

- V1-SET-005

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test tests/Host/Experience/KitchenOperations` → gerçek
  Postgres'e karşı yeşil; en az bir yeni test — ayar hiç kayıtlı değilken
  varsayılan `9` döndüğü, `SettingsService.SetValueAsync` ile
  değiştirilince aynı uçtan yeni değerin döndüğü.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → `clean`.
- Semih'in elle deneyebileceği senaryo: ayarı `15` yap, Mutfak
  ekranından `/operations/live-sync`'i çek, yanıtta eşiğin `15`
  göründüğünü doğrula.

## Handoff

- V1-KDS-006

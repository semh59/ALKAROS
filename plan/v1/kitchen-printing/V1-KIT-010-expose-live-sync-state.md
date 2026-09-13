# V1-KIT-010 - kitchen.live_sync_enabled durumunu Kitchen HTTP yüzeyine aç

- Task ID: V1-KIT-010
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

Bağımsız denetimde (2026-09-13, `[[kitchen-redesign-requirements]]`)
tekrar bulunan, daha önce de işaretlenmiş bir boşluk: `kitchen.live_sync_enabled`
(varsayılan kapalı) kapalıyken mutfak kalemi hazır olduğunda garsona
bildirim gitmiyor, ama bu davranış farkı Mutfak ekranında hiçbir yerde
görünmüyor — sessizce farklı çalışıyor. Bugün `KitchenOperationsStore.cs:156`
zaten `KitchenLiveSyncSetting.IsEnabledAsync(_settings, ...)` ile bu
değeri okuyor (transition işlerken); bu görev yalnız aynı değeri
**okunabilir** hale getirir — Kitchen HTTP contract'ında hiçbir yerde
taşınmıyor.

## Owned surface

- src/Host/Experience/KitchenOperations/KitchenOperationsContracts.cs,
  KitchenOperationsEndpoints.cs, KitchenOperationsStore.cs (Sınırlı ek —
  V1-RMD-082 sahipliğinde kalan dosyalar) — mevcut bir GET yanıtına
  (ör. `/operations/health/latest` veya yeni küçük bir alan) `liveSyncEnabled: bool`
  eklenmesi; hangi uca ekleneceği bu görevde kararlaştırılır (yeni bir uç
  açmak yerine zaten `load()`'ın çağırdığı bir GET'e eklemek tercih
  edilir — gereksiz bir ağ isteği eklenmesin).
- tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
  (Sınırlı ek) — yeni test.

## Out of scope

- Frontend gösterimi (`V1-KDS-004`).
- Ayarı değiştirme (yalnız okuma; toggle zaten Settings modülünde var).

## Dependencies

- None

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test` → yeni test dahil ilgili proje yeşil, gerçek Postgres'e
  karşı; ayar açıkken/kapalıyken dönen değerin doğru olduğu kanıtlanır.
- Semih'in elle deneyebileceği senaryo: ayarı kapalıyken Mutfak
  verisini çek, yanıtta `liveSyncEnabled: false` görünsün; ayarı açıp
  tekrar çek, `true` dönsün.

## Handoff

- None

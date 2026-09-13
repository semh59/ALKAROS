# V1-KIT-010 - kitchen.live_sync_enabled durumunu Kitchen HTTP yüzeyine aç

- Task ID: V1-KIT-010
- Status: Done
- Assignee: Claude Sonnet 5
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

- `dotnet build ALKAROS.slnx -c Debug` → **0 Uyarı, 0 Hata.**
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet
  test tests/Host/Experience/KitchenOperations/ALKAROS.Host.Experience.KitchenOperations.Tests.csproj
  -c Release` → **Başarılı! Başarısız: 0, Başarılı: 13, Atlanan: 0,
  Toplam: 13** (12 mevcut + 1 yeni:
  `LiveSyncStatusReflectsTheDeploymentSettingDefaultOffThenOn`, gerçek
  Postgres'e karşı — ayar hiç kayıtlı değilken varsayılan `false`
  döndüğü, `SettingsService.SetValueAsync` ile `true` yapılınca aynı
  uçtan `true` döndüğü kanıtlandı).
- `python tools/consistency-audit/consistency_audit.py` → `clean`.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- Uygulanan tasarım: yeni `GET /kitchen-operations/operations/live-sync`
  ucu (`LiveSyncStatusV1(bool Enabled)`), `KitchenOperationsStore
  .GetLiveSyncStatusAsync` zaten var olan `KitchenLiveSyncSetting
  .IsEnabledAsync`'i çağırıyor — `TransitionItemAsync`'in kullandığı
  aynı okuma yolu. `HealthSnapshotV1`'e eklenmedi (health snapshot
  `null` olabilir, ayar durumu ondan bağımsız olmalı).
- Semih'in elle deneyebileceği senaryo: ayarı kapalıyken Mutfak
  ekranından `/operations/live-sync`'i çek, `{"enabled":false}` görünsün;
  ayarı açıp tekrar çek, `{"enabled":true}` dönsün.

## Handoff

- None

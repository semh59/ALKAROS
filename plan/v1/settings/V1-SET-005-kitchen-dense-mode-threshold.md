# V1-SET-005 - Mutfak yoğun mod eşiği ayarı

- Task ID: V1-SET-005
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

Mutfak ekranının otomatik yoğun-mod eşiği (`AUTO_DENSE_OPEN_ITEM_THRESHOLD`,
şu an `KitchenOperationsWorkspace.tsx`'te sabit `9`) işletmeler arasında
farklılık gösterir — küçük bir kahvaltı büfesi için 9 açık kalem zaten
yoğun, büyük bir mutfak için hiç değil. `V1-SET-002`'nin (kitchen.
live_sync_enabled) aynı deseni: tek bir tipli ayar, varsayılan değer
bugünkü sabitle birebir aynı (`9`) olduğu için hiçbir davranış değişmez,
bir işletme yalnız isterse değiştirir.

## Owned surface

- `plan/v1/settings/V1-SET-005-kitchen-dense-mode-threshold.md`
- `src/Modules/Settings/KitchenDenseModeThreshold/**`
- `tests/Modules/Settings/KitchenDenseModeThreshold/**`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## In scope

- `KitchenDenseModeThresholdSetting.Key = "kitchen.dense_mode_threshold"`
  sabiti, `SettingDataType.WholeNumber`, `SettingScope.Global`,
  `module_owner = "kitchen"`, varsayılan değer `"9"` (bugünkü sabitle
  birebir aynı).
- `EnsureRegisteredAsync(ISettingsService, CancellationToken)`:
  `V1-SET-002`'nin kendi deseniyle birebir aynı — anahtar yoksa kaydeder,
  `DuplicateSettingKeyException` yakalanır (no-op).
- `GetThresholdAsync(ISettingsService, CancellationToken)`: anahtar hiç
  kayıtlı değilse ilk soruluşta kendini kaydedip `9` döner; kayıtlıysa
  `GetValueOrDefaultAsync<int>(Key, 9)`.

## Out of scope

- Ayarı değiştirecek bir yönetici ekranı — genel Settings API'si
  (`SetValueAsync`) zaten mevcut, kitchen-özel bir UI bu görevin
  kapsamında değildir.
- Kitchen HTTP yüzeyinden bu değerin okunabilir olması (`V1-KIT-013`).
- Değer için alt/üst sınır zorlaması (0 veya negatif bir eşik anlamsız
  olsa da `SettingValidator.ValidateValue` yalnız "geçerli 64-bit tamsayı
  mı" kontrolü yapıyor, `WholeNumber` için ek bir aralık kısıtı bu görevde
  eklenmiyor — mevcut tiplerin hiçbiri kendi aralık kısıtını
  taşımıyor, bu görev o deseni bozmuyor).

## Dependencies

- V1-SET-001

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test tests/Modules/Settings/KitchenDenseModeThreshold` → gerçek
  Postgres'e karşı yeşil (V1-SET-002'nin 4 testiyle birebir aynı desende:
  ilk soruluşta `9` ile kendini kaydeder; ikinci `EnsureRegisteredAsync`
  çağrısı no-op; operatör değiştirdiğinde `GetThresholdAsync` bunu
  yansıtır; anahtar hiç dokunulmamışsa varsayılan `9`).
- `dotnet test tests/Modules/Settings/TypedSettings` → regresyon yok.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0
  uyarı.
- `python tools/consistency-audit/consistency_audit.py` → `clean`.

## Handoff

- V1-KIT-013

# V1-SET-002 - Kitchen live-sync feature toggle

- Task ID: V1-SET-002
- Status: Blocked
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Goal

`V1-IAM-026`'nın kaydettiği görev kümesinin ilk halkası: bir kurulumda mutfak
bilet durumunun Sipariş kaydına gerçekten senkronize edilip edilmeyeceğini
(ve buna bağlı garson bildirimi ile gönderildi-ama-servis-edilmedi void
yolunun) belirleyen tek bir açma/kapama anahtarı. Varsayılan **kapalı** —
kapalıyken hiçbir davranış değişmez (bugünkü sistemle birebir aynı). `V1-SET-001`
(tipli ayarlar deposu) hazır ama şu ana kadar hiçbir modül tarafından
tüketilmiyordu; bu görev onun ilk gerçek tüketicisidir.

## Owned surface

- `plan/v1/settings/V1-SET-002-kitchen-live-sync-toggle.md`
- `src/Modules/Settings/KitchenLiveSync/**` (yeni, `TypedSettings`'e komşu
  ayrı bir alt dizin — `ISettingsService`'i tüketir, `TypedSettings`'in
  kendi dosyalarına dokunmaz; anahtar adı sabiti + `EnsureRegisteredAsync`
  yardımcı, `RegisterSettingAsync` zaten var olan bir anahtarda
  `DuplicateSettingKeyException` fırlatır, bu yüzden idempotent bir
  sarmalayıcı gerekir)
- `tests/Modules/Settings/KitchenLiveSync/**` (yeni)
- `evidence/V1-SET-002/**`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## In scope

- `KitchenLiveSyncSetting.Key = "kitchen.live_sync_enabled"` sabiti.
- `EnsureRegisteredAsync(ISettingsService, CancellationToken)`: anahtar yoksa
  `RegisterSettingAsync` ile `Boolean`, `Global` scope, `module_owner =
  "kitchen"`, değer `"false"` olarak kaydeder; `DuplicateSettingKeyException`
  yakalanır (zaten kayıtlıysa no-op).
- `IsEnabledAsync(ISettingsService, CancellationToken)`:
  `GetValueOrDefaultAsync<bool>(Key, false)` sarmalayıcısı — tüketici
  modüller (`V1-KIT-005`, `V1-IAM-027`) bunu çağırır.
- Host başlangıcında `EnsureRegisteredAsync`'in çağrılacağı satır (muhtemelen
  `SettingsModule.Register` veya bir composition-root startup adımı) —
  dokunulacak dosya implementasyon sırasında netleşir, Blocker'da not edilir.

## Out of scope

- Ayarı değiştirecek bir yönetici ekranı — `GetValueOrDefaultAsync` /
  `SetValueAsync` zaten var olan genel Settings API'si üzerinden mevcut;
  kitchen-özel bir UI bu görevin kapsamında değil.
- `V1-KIT-005`, `V1-WTR-009`, `V1-IAM-027`'nin kendi davranışı.

## Dependencies

- V1-SET-001

## Blocker

- Host başlangıç noktasının (muhtemelen `src/Host/Composition/**` veya
  `Program.cs`) tam olarak hangi satırına `EnsureRegisteredAsync` çağrısı
  ekleneceği implementasyon sırasında netleşir; o dosyanın sahibi görevine
  sınırlı ek notu eklenmelidir. Ancak bu netleştiği ve gerekirse custody
  notu eklenip `validate` temiz kaldığında görev `Planned` yapılabilir.

## Acceptance evidence

- (implementasyon tamamlandıktan sonra doldurulur.)

## Handoff

- V1-KIT-005

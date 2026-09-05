# V1-SET-002 - Kitchen live-sync feature toggle

- Task ID: V1-SET-002
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
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
- `src/Modules/Settings/KitchenLiveSync/**`
- `tests/Modules/Settings/KitchenLiveSync/**`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## In scope

- `KitchenLiveSyncSetting.Key = "kitchen.live_sync_enabled"` sabiti.
- `EnsureRegisteredAsync(ISettingsService, CancellationToken)`: anahtar yoksa
  `RegisterSettingAsync` ile `Boolean`, `Global` scope, `module_owner =
  "kitchen"`, değer `"false"` olarak kaydeder; `DuplicateSettingKeyException`
  yakalanır (zaten kayıtlıysa no-op, işletmenin seçtiği değeri asla ezmez).
- `IsEnabledAsync(ISettingsService, CancellationToken)`: anahtar hiç
  kayıtlı değilse ilk soruluşta kendini kaydedip `false` döner (ayrı bir
  Host başlangıç kancasına gerek yok); kayıtlıysa
  `GetValueOrDefaultAsync<bool>(Key, false)`.

## Out of scope

- Ayarı değiştirecek bir yönetici ekranı — `GetValueOrDefaultAsync` /
  `SetValueAsync` zaten var olan genel Settings API'si üzerinden mevcut;
  kitchen-özel bir UI bu görevin kapsamında değildir.
- `V1-KIT-005`, `V1-WTR-009`, `V1-IAM-027`'nin kendi davranışı.

## Dependencies

- V1-SET-001

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` ve `-c Debug`: 0 uyarı / 0 hata.
- `dotnet test` (yerel Postgres 18, `alkaros-test-pg`):
  `ALKAROS.Settings.KitchenLiveSync.Tests` 4/4 — ilk soruluşta `false` ile
  kendini kaydeder; ikinci `EnsureRegisteredAsync` çağrısı no-op (satır
  değişmez); operatör `true` yaptığında `IsEnabledAsync` bunu yansıtır;
  anahtar hiç dokunulmamışsa varsayılan `false`.
  `ALKAROS.Settings.TypedSettings.Tests` 33/33 (regresyon yok).
- `python tools/project-manifest/project_manifest_tool.py`: VALID.
- Ortam istisnası: Release konfigürasyonunda yeni derlenen test DLL'i bu
  oturumda geçici bir WDAC engeline takıldı (G1, önceden belgeli); temiz
  bir `bin`/`obj` silme + yeniden derleme sonrası Debug'da 4/4 geçti; CI
  test otoritesidir.

## Handoff

- V1-KIT-005

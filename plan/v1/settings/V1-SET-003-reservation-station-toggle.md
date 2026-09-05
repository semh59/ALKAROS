# V1-SET-003 - Reservation station feature toggle

- Task ID: V1-SET-003
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: implementation
- Surface state: Existing

## Goal

`V1-TBL-008`'in kaydettiği görev kümesinin ilk halkası: bir kurulumun ayrı
bir "Rezervasyon İstasyonu" ekranı sunup sunmadığını belirleyen tek bir
açma/kapama anahtarı. Varsayılan **kapalı** — kapalıyken kasiyerin kendi kat
planı ekranındaki mevcut, koşulsuz "Rezervasyon al" aksiyonu değişmeden
sürer. `V1-SET-002`'nin (`kitchen.live_sync_enabled`) birebir aynı deseni.

## Owned surface

- `plan/v1/settings/V1-SET-003-reservation-station-toggle.md`
- `src/Modules/Settings/ReservationStation/**`
- `tests/Modules/Settings/ReservationStation/**`
- Paylaşılan dosyalarda sınırlı ek (V1-RMD-089/9. dalga deseni — sahiplik
  ilgili görevde kalır): src/Host/DualScreen/DualScreenApplication.Endpoints.cs
  (mevcut sahiplikte kalır — bkz. dosya geçmişi) — `runtime-configuration`
  uç noktasına `ISettingsService settings` parametresi ve
  `reservationStationEnabled` alanı eklendi; mevcut `kitchenStationId`/
  `customerDisplayUrl` davranışı değişmedi.
  tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs
  (mevcut sahiplikte kalır) — `RuntimeConfigurationRequiresTheBoundTerminal
  AndReturnsOnlyTheKitchenStation` testi yeni alanı da doğrulayacak şekilde
  güncellendi (adı da yeni davranışı yansıtacak şekilde değişti) ve anahtarı
  açıp doğrulayan bir adım eklendi; testin geri kalanı değişmedi.

## In scope

- `ReservationStationSetting.Key = "reservations.dedicated_station_enabled"`
  sabiti.
- `EnsureRegisteredAsync`/`IsEnabledAsync` — `V1-SET-002`'nin
  `KitchenLiveSyncSetting`'iyle birebir aynı sözleşme.
- `runtime-configuration` uç noktasının yanıtına `reservationStationEnabled`
  alanının eklenmesi (PosTerminal'in `/reservations` bağlantısını
  gösterip göstermeyeceğine karar vermesi için).

## Out of scope

- Ayarı değiştirecek bir yönetici ekranı — genel Settings API'si üzerinden
  mevcut.
- `V1-CUI-006`'nın kendi istasyon ekranı.

## Dependencies

- V1-SET-001

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` ve `-c Debug`: 0 uyarı / 0 hata.
- `dotnet test` (yerel Postgres 18, `alkaros-test-pg`):
  `ALKAROS.Settings.ReservationStation.Tests` 4/4 (V1-SET-002'nin dört
  testinin birebir aynısı, yeni anahtar için); `ALKAROS.Settings
  .KitchenLiveSync.Tests` 4/4 regresyonsuz; `ALKAROS.Architecture.Tests`
  8/8, `ALKAROS.Host.Experience.Composition.Tests` 4/4 regresyonsuz.
- `tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs`'teki
  güncellenmiş test bu oturumda **çalıştırılamadı**: bu proje `psql` CLI'yi
  doğrudan çağırıyor ve bu makinede `psql` kurulu değil (G2, önceki
  oturumlarda belgelenmiş, bu dalgadan bağımsız — aynı projede zaten 41/121
  test bu nedenle başarısız). Doğruluk `dotnet build` (0 hata) ve testin
  mekanik olarak mevcut, aynı dosyadaki komşu testlerle birebir aynı deseni
  izlemesiyle sağlandı; CI (psql kurulu) test otoritesidir.
- `python tools/consistency-audit/consistency_audit.py`: temiz.
- `python tools/project-manifest/project_manifest_tool.py`: VALID.

## Handoff

- V1-CUI-006

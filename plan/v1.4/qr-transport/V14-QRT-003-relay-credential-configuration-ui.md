# V14-QRT-003 - Relay credential configuration from the interface

- Task ID: V14-QRT-003
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-07

## Goal

Semih'in "domain kurulumu için kod tabanına dönmek istemem" talebini
karşılamak: relay sağlayıcısının (Cloudflare) API anahtarını arayüzden,
yönetici girişiyle, güvenli şekilde girip güncelleyebilmek — anahtar
veritabanına yalnız şifreli (AES-256-GCM zarf) olarak yazılır, hiçbir
endpoint onu geri döndürmez.

## Owned surface

- `src/Modules/QrOrdering/RelayCredential/**` (yeni)
- `src/Host/Experience/RelaySettings/**` (yeni)
- `tests/Modules/QrOrdering/RelayCredential/**`, `tests/Host/Experience/RelaySettings/**` (yeni)
- `src/Clients/PosTerminal/src/routes/RelaySettings.tsx`,
  `src/Clients/PosTerminal/src/routes/RelaySettings.test.tsx` (yeni)
- `database/migrations/V14/V14-QRT-003/**` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - src/BuildingBlocks/Security/Secrets/EnvironmentVariableSecretProvider.cs
    (V1-SEC-001 sahipliğindeki dizin ön ekinin altında, yeni dosya) — bu
    building block'un ilk gerçek production `ISecretProvider`'ı;
    `ALKAROS.Secrets`/`ALKAROS.SensitiveData` daha önce hiç kullanılmıyordu.
  - src/Modules/QrOrdering/ALKAROS.QrOrdering.csproj,
    src/Modules/QrOrdering/TokenLifecycle/QrOrderingModule.cs
    (V14-QRS-001 sahipliğinde) — yalnız Secrets/SensitiveData proje
    referansları ve yeni servis kayıtları eklendi.
  - src/Modules/Identity/Authorization/Catalog/ApplicationPermissions.cs
    (V1-IAM-017 sahipliğinde) — yalnız `integrations.manage` (ilk
    yönetici-özel yetki) eklendi.
  - tests/Modules/Identity/Authorization/Catalog/ApplicationPermissionsTests.cs,
    PermissionSplitDatabase.cs, PermissionSplitDatabaseTests.cs (V1-IAM-017
    sahipliğinde) — yeni katalog sayısı (16) ve "manager artık supervisor'ın
    üstünde" gerçeğini yansıtacak şekilde güncellendi; `PermissionSplitDatabase`
    migration 079'u da uygulayıp geri alıyor.
  - src/Host/DualScreen/DualScreenApplication.cs (V1-IAM-024 sahipliğinde)
    — yalnız `AddRelaySettingsExperience`/`MapRelaySettingsApi` çağrıları
    eklendi.
  - database/MigrationComposition/order.json,
    src/Host/Composition/Migrations/MigrationManifest.cs,
    tests/Host/MigrationComposition/Manifest/ManifestTests.cs
    (V1-FND-004/V1-IAM-025 sahipliğinde) — yalnız migration 079/080
    kayıtları eklendi.
  - src/Clients/PosTerminal/src/api.ts, contracts.ts, App.tsx (birikimli
    olarak birçok görevin sahipliğinde) — yalnız yeni relay-credential
    çağrıları, tip ve `/settings/relay` route'u eklendi.
  - ALKAROS.slnx (yalnız yeni proje kayıtları için).

## In scope

- `ISecretProvider`'ın gerçek (env-var tabanlı) implementasyonu.
- `RelayCredentialAccessPolicy`: hem `ISecretAccessPolicy` hem
  `ISensitiveDataAccessPolicy` — yalnız bu store'un kendi accessor kimliği
  okuyabilir/şifre çözebilir.
- `qr_ordering.relay_credentials`: tek satırlık, AES-256-GCM zarf (ciphertext
  dışında hiçbir şey) — ham anahtar asla veritabanına yazılmaz.
- `integrations.manage`: kataloğun ilk yönetici-özel (supervisor'ın bile
  almadığı) yetkisi — bu, tek seferlik bir kurulum eylemi, bills.void/comp
  gibi işlem-bazlı bir istisna değil.
- `POST .../relay-credential/` (kaydet, 204, hiçbir zaman değeri döndürmez),
  `GET .../relay-credential/status` (yalnız configured/updatedAt).
- `/settings/relay`: yönetici girişi + durum kartı + anahtar girme formu.

## Out of scope

- Cloudflare API'sine gerçekten tünel/DNS oluşturma çağrısı yapmak
  (`V14-QRT-001`'in kapsamı — bu görev yalnız o otomasyonun okuyacağı
  anahtarı güvenle saklamayı sağlıyor).
- Yönetim/Arka ofis modülünün gerçek navigasyonu — `/settings/relay` şimdilik
  `/reservations`/`/display` gibi kendi başına bir URL.

## Dependencies

- V14-QRT-002

## Deliverables

- Yukarıdaki Owned surface'teki production code ve testler.

## Acceptance evidence

- Gerçek regresyon bulundu ve düzeltildi: `manager`'ı `supervisor`'ın
  bir üstüne çıkarmak, katalogda halihazırda var olan
  "supervisor ve manager aynı kümeyi tutar" testini kırdı
  (`ApplicationPermissionsTests`/`PermissionSplitDatabaseTests`, 4 test).
  Bu testler yeni gerçeği (manager = supervisor + `integrations.manage`)
  yansıtacak şekilde güncellendi; kasıtlı olarak kırılan eski invariant
  kayda geçirildi, sessizce atlanmadı.

- Semih'in elle deneyebileceği senaryo: `/settings/relay`'e yönetici
  hesabıyla girilir, "Henüz yapılandırılmadı" görünür, bir anahtar girilip
  kaydedilir, "Yapılandırıldı" durumuna geçer; anahtarın kendisi hiçbir
  ekranda veya ağ isteğinde geri görünmez.
- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `ALKAROS.QrOrdering.RelayCredential.Tests`: 5/5 (round-trip, veritabanı
  sızıntısında ham değer yok, ikinci kayıt öncekini değiştirir, yanlış
  erişim politikasıyla şifre çözme reddi).
- `ALKAROS.Host.Experience.RelaySettings.Tests`: 6/6 (oturumsuz 401,
  cashier/supervisor 403, manager 204+status, boş anahtar 400, değer hiçbir
  cevapta geri dönmüyor).
- PosTerminal: `tsc --noEmit` temiz, `vitest run` 21 dosya/130 test yeşil
  (3'ü yeni), `vite build` başarılı.
- `python tools/consistency-audit/consistency_audit.py`: 13 ihlal, hepsi
  önceden vardı (kendi eklediğim bir Türkçe-yorum ihlali bulunup düzeltildi).
- `python tools/plan-audit/plan_audit_tool.py validate`: sıfır hata.

## Handoff

- V14-QRT-001

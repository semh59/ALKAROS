# V1-SET-008 - Business logo upload for the QR customer pages

- Task ID: V1-SET-008
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`V1-SET-007`'nin kendi "Out of scope"unda ertelediği üç maddeden ilki:
işletmenin QR sipariş sayfalarına kendi logosunu yükleyebilmesi
("restoran ve logo yüklesin" — Semih'in orijinal isteği). Mimari emsal
`V1-CDP-001`'in ekran koruyucu görseli — tek global satır, bytea depolama,
istemci-beyanından bağımsız magic-number doğrulaması, açık boyut sınırı,
Kestrel body-size middleware override, ETag/Cache-Control/304 — birebir
aynı desen burada da uygulandı.

Yönetici yazma yüzeyi için, ekran koruyucunun `catalog.manage`'ı yeniden
kullanmasının aksine, `V1-RMD-246`'nın business.name/business.accent_theme
için zaten kurduğu `settings.manage` manager-gate'i (`SettingsManagerEndpointFilter`)
yeniden kullanıldı — logo da bir business-identity ayarı, Catalog'un değil.

## Owned surface

- `database/migrations/V1/V1-SET-008/**` (yeni)
- `evidence/V1-SET-008/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Settings/BusinessIdentity/
  (V1-SET-007 sahipliğinde) — yeni `BusinessLogoStore.cs`/`IBusinessLogoStore`.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Settings/SettingsModule.cs
  (V1-RMD-002 sahipliğinde) — `IBusinessLogoStore` kaydı eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Settings/BusinessIdentity/
  (V1-SET-007 sahipliğinde) — yeni `BusinessLogoStoreTests.cs`, migration 136
  fixture eklemesi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Settings/
  (V1-RMD-246 sahipliğinde) — yeni `BusinessIdentityLogoEndpoints.cs`
  (`PUT`/`DELETE /api/v1/management/business-identity/logo`).
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Settings/
  (V1-RMD-246 sahipliğinde) — yeni `BusinessIdentityLogoEndpointsTests.cs`.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/QrOrdering/QrOrderingEndpoints.cs
  (V12-CWB-001 sahipliğinde) — yeni `GET /api/v1/qr/logo` (oturumsuz,
  public), `/branding`'in `HasLogo` alanı artık gerçek değeri okur.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/QrOrdering/QrOrderingHttpTests.cs,
  ALKAROS.Host.Experience.QrOrdering.Tests.csproj (V12-CWB-001 sahipliğinde) —
  yeni `/logo` testleri, migration 136 fixture eklemesi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs
  (çok sayıda geçmiş dalga görevinin sahipliğinde) — servis kaydı zincirine
  `builder.Services.AddBusinessIdentityLogoExperience();`, route eşleme
  zincirine `app.MapBusinessIdentityLogoApi();` eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs,
  tests/Host/MigrationComposition/Manifest/ManifestTests.cs — migration 136
  pozisyonunun kaydı (`settings.business_logo`).

## In scope

1. Migration: `settings.business_logo` (V1-CDP-001'in
   `customer_display.screensaver_images`'ıyla birebir aynı şema — `id=1`
   check constraint, `content BYTEA`, `content_type`, `updated_at`,
   `row_version`).
2. `PUT /api/v1/management/business-identity/logo` — `settings.manage`
   yetkisi, `multipart/form-data`, yalnız `image/png`/`image/jpeg`/`image/webp`,
   azami 5 MB, magic-number doğrulaması, reddedilirse Türkçe sebep.
3. `DELETE /api/v1/management/business-identity/logo` — logoyu kaldırır.
4. `GET /api/v1/qr/logo` — oturumsuz, public (aynı `/branding` gerekçesi:
   sayfanın ilk render'ı oturum kurulmadan önce ihtiyaç duyuyor), logo
   yoksa `404` (placeholder değil), ETag/`Cache-Control: public, max-age=300`.
5. `QrBrandingResponse.HasLogo` artık gerçek depo durumunu yansıtır (sabit
   `false` değil — V1-SET-007'nin kendi ertelemesiydi).

## Out of scope

- CustomerWeb'in bu uç noktaları gerçekten TÜKETMESİ (üç sayfada logo
  gösterimi) — V1-SET-007 ile aynı gerekçe, ayrı bir görev.
- Logo kırpma/yeniden boyutlandırma — istemci olduğu gibi gösterir
  (V1-CDP-002'nin ekran koruyucu için yaptığı CSS çözümüyle aynı kapsam
  dışı karar).
- PosTerminal'de bir yönetici yükleme ekranı — bu görev yalnız gerçek,
  çağrılabilir backend ucunu açar (V1-CDP-001'in kendi "yönetici yükleme
  ekranı V1-CDP-003'ün kapsamı" kararıyla aynı).

## Dependencies

- V1-SET-007
- V1-RMD-246

## Acceptance evidence

- Gerçek Postgres + gerçek Host'a karşı HTTP testi: `settings.manage`
  izni olmayan/oturumsuz `PUT` → 401/403; izin verilmeyen MIME türü → 400
  (DB'ye yazılmadı); 5 MB üstü dosya → 400 (DB'ye yazılmadı); beyan edilen
  türle eşleşmeyen içerik (magic-number) → 400 (DB'ye yazılmadı); gerçek
  bir PNG yüklenip `DELETE` ile kaldırılması; `GET /api/v1/qr/logo` logo
  yokken 404, yüklendikten sonra aynı baytları ve `Content-Type`'ı
  döndürmesi, `If-None-Match` ile 304; `GET /api/v1/qr/branding`'in
  `HasLogo`'sunun gerçek depo durumunu (false → true) yansıtması.
- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- Migration boş bir veritabanında ileri/geri (up/down) denenir.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- Semih'in elle deneyebileceği senaryo: `settings.manage` izni olan bir
  yönetici oturumuyla `PUT /api/v1/management/business-identity/logo`'ya
  gerçek bir PNG yükle, `GET /api/v1/qr/logo`'nun aynı görseli döndürdüğünü
  gör; `DELETE` ile kaldır, `GET`'in 404 döndüğünü ve `GET /api/v1/qr/branding`'in
  `HasLogo:false`'a döndüğünü doğrula.

## Handoff

- None

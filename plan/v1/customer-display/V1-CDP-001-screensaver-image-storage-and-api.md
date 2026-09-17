# V1-CDP-001 - Müşteri ekranı ekran koruyucu görseli: depolama ve uç noktalar

- Task ID: V1-CDP-001
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-16

## Goal

İşletmenin, müşteri ekranı boşta iken (Idle durumu) ALKAROS'un sabit markalı
kartı yerine kendi seçtiği bir görseli (logo, kampanya afişi vb.) gösterebilmesi
için: bir yönetici-only yükleme/kaldırma uç noktası ve müşteri ekranının
kendi read-only yetkisiyle çekebileceği bir sunma uç noktası. Tek işletme/tek
kurulum modeline uygun olarak TEK küresel görsel (terminale göre değil) —
bugünkü hiçbir modülde per-terminal marka ayrımı yok, icat edilmiyor.

## Owned surface

- `src/Host/DualScreen/DualScreenStore.Screensaver.cs` (yeni) — depolama/okuma.
- `src/Host/DualScreen/DualScreenScreensaverContracts.cs` (yeni) — DTO'lar.
  (`DualScreenContracts.cs`'e eklenmiyor — o dosya V1-WTR-054'ün sahipliğinde,
  bu görev onu yeniden açmaz.)
- `database/migrations/V1/V1-CDP-001/**` (yeni) —
  `customer_display.screensaver_images` tablosu (tek satır, bytea içerik).
- `evidence/V1-CDP-001/**`
- PO:2026-09-16 kararıyla src/Host/DualScreen/DualScreenApplication.Screensaver.cs
  ve tests/Host/MigrationComposition/DualScreen/DualScreenScreensaverTests.cs
  yüzeyi V1-CDP-004'e devredildi; bu historical task closed kalır.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs
  içine tek satır eklenir — `app.MapCustomerDisplayScreensaverApi();`
  (`MapRelaySettingsApi()` vb. ile aynı desen); bu dosya zaten onlarca
  `MapXxxApi()` çağrısı biriktiren, birçok görev tarafından paylaşılan bir
  kompozisyon kökü.
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs ve
  tests/Host/MigrationComposition/Manifest/ManifestTests.cs — migration 119
  kaydı, `PhaseBMax` güncellemesi ve testin sabit id/count listesine 119
  eklenmesi (V11-RCP-004'ün migration 116 için yaptığıyla aynı desen); üçü
  de zaten her yeni migration eklendiğinde dokunulan, birçok görev
  tarafından paylaşılan dosyalar.

## In scope

1. Migration: `customer_display.screensaver_images` (id sabit tek satır ya da
   tekil constraint, `content BYTEA NOT NULL`, `content_type VARCHAR(64) NOT NULL`,
   `updated_at TIMESTAMPTZ`, `row_version INTEGER`).
2. `PUT /api/v1/management/customer-display/screensaver` — yönetici yetkisi
   (`catalog.manage`, System-health'in kullandığı yetkiyle aynı — bkz. karar
   notu), `multipart/form-data`, yalnız `image/png`/`image/jpeg`/`image/webp`,
   azami 5 MB; reddedilirse Türkçe, anlaşılır bir sebep döner.
3. `DELETE /api/v1/management/customer-display/screensaver` — görseli kaldırır,
   ekran varsayılan markalı karta döner.
4. `GET /api/v1/customer-displays/{displayId}/screensaver` — display principal'ın
   kendi `customer-display:read` yetkisiyle erişebildiği, ham görsel baytlarını
   `Content-Type` ile döndüren; görsel yoksa `404` (istemci varsayılana düşer).
   `Cache-Control`/`ETag` — istemci her Idle geçişinde tekrar tekrar aynı
   görseli çekmesin.

## Out of scope

- Per-terminal veya çoklu görsel/slayt gösterisi — tek görsel, tek işletme.
- Görsel kırpma/yeniden boyutlandırma — istemci olduğu gibi gösterir
  (V1-CDP-002'nin `object-fit` gibi CSS çözümleri kapsamında).
- Yönetici yükleme ekranı — V1-CDP-003'ün kapsamı.

## Dependencies

- None

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` → 0 Uyarı, 0 Hata (tam çözüm, tüm
  modüller dahil).
- Gerçek Postgres'e karşı (`ALKAROS_TEST_PG_PORT=55432`)
  `dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj
  --filter "FullyQualifiedName~DualScreenScreensaverTests|FullyQualifiedName~ManifestTests"`
  → 26/26 geçti (10 yeni `DualScreenScreensaverTests` + 16 `ManifestTests`,
  migration 119 kaydının manifest'i bozmadığını doğruluyor). Kapsanan
  senaryolar: store seviyesinde kaydet/oku round-trip, ikinci kaydın tekil
  satırı `INSERT` değil `UPDATE` ile değiştirdiğinin kanıtı (satır sayısı 1),
  silme sonrası `null`; HTTP seviyesinde yönetici oturumu olmadan `PUT` →
  401, display oturumu olmadan `GET` → 401, izin verilmeyen MIME türü
  (`application/pdf`) → 400 ve DB'ye hiçbir şey yazılmadığının doğrulanması,
  5 MB üstü dosya → 400 ve DB'ye yazılmadığının doğrulanması, gerçek
  yönetici+display oturumlarıyla `PUT` → `GET` aynı baytları ve
  `Content-Type`'ı geri döndürüyor, silme sonrası `GET` → 404.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı
  (865 markdown, 843 task, 1955 bağımlılık kenarı).
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `git status --short` → yalnız Owned surface'ta tanımlı dosyalar ve
  paylaşılan "Sınırlı ek" yolları (`DualScreenApplication.cs`, `order.json`,
  `MigrationManifest.cs`, `ManifestTests.cs`) değişti; kapsam dışı hiçbir
  dosyaya dokunulmadı.
- Bulunan ve düzeltilen gerçek bir çevre/tasarım noktası: uç nokta zinciri
  HTTPS'i zorunlu kılıyor (`DualScreenApplication.cs`'in mevcut HTTPS
  ön-denetimi, bu görevden önce de vardı); test istemcisi bu yüzden gerçek
  bir ters-proxy senaryosunu taklit etmek için `TrustedProxies:
  [IPAddress.Loopback]` + `X-Forwarded-Proto: https` başlığıyla istek
  gönderiyor — `DualScreenHostTests.cs`'teki mevcut `CreateForwardedRequest`
  deseniyle aynı, icat edilmiş bir istisna değil.
- Semih'in elle deneyebileceği senaryo: bir PNG yükle
  (`PUT /api/v1/management/customer-display/screensaver`, yönetici
  çerezi ile), müşteri ekranının fetch ettiği
  `GET /api/v1/customer-displays/{displayId}/screensaver` uç noktasının
  aynı görseli döndürdüğünü doğrula; `DELETE` ile kaldır, `GET`'in 404
  döndüğünü doğrula.

## Handoff

- V1-CDP-002
- V1-CDP-003
- V1-CDP-004

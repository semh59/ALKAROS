# V1-SET-007 - Business name + a pre-vetted accent color for QR ordering pages

- Task ID: V1-SET-007
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in 2026-09-19 ürün kararı (bu oturumda, gerçek rakip ekranlarıyla
doğrulandı — Menulux Tablet Menu başlığı "Menulux Restaurant" değil kendi
demo işletme adını gösteriyor, Lightspeed Self-Order'da hiç yazılım
markası yok): **QR sipariş sayfaları ALKAROS'un değil, işletmenin kendi
kimliğine ait olmalı.** İkinci karar: renk serbest hex değil, **önceden
WCAG kontrastı doğrulanmış bir paletten** seçilir — "saçma sapan renk
seçip şikayet etmesin."

Bu görev, `docs/design/foundations.md`'nin kendi WCAG doğrulama disiplinini
(§1.3) CustomerWeb'in gerçek kullanım biçimine uygulayarak (`--cw-accent`
hem düz metin hem `--cw-accent-contrast` beyazının üzerine bindiği dolgu
olarak kullanılıyor — üç uygulamanın gerçek CSS'i okunarak doğrulandı) 8
renklik bir palet üretiyor; her biri hem "beyaz üzerinde metin" hem "renk
dolgusu üzerinde beyaz metin" için ayrı ayrı ≥4.5:1 (WCAG AA) hesaplanıp
doğrulandı (gerçek relative-luminance/contrast formülüyle, tahmin değil).
**Mevcut üretim rengi (#B5772F) bu barajı GEÇEMİYOR (3.72:1)** — palette
bunun yerine aynı ton ailesinin koyulaştırılmış, barajı geçen hali
(#9C6323, 4.97:1) dahil edildi; bu, ayrı bir not olarak kayıtlı (bkz.
Out of scope).

Mimari emsal `V1-CDP-001`'in kendi kararı: "tek global satır, hiçbir
kurulum henüz çok şubeli değil" — işletme adı/rengi de aynı şekilde tek,
global.

## Owned surface

- `src/Modules/Settings/BusinessIdentity/**` (yeni)
- `tests/Modules/Settings/BusinessIdentity/**` (yeni)
- `evidence/V1-SET-007/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/QrOrdering/QrOrderingEndpoints.cs
  (V12-CWB-001 sahipliğinde) — yeni `GET /branding` route'u eklenir
  (oturumsuz, public — bkz. In scope). Mevcut route'lar değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/QrOrdering/QrOrderingContracts.cs
  (V12-CWB-001 sahipliğinde) — yeni `QrBrandingResponse` record'u eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/QrOrdering/QrOrderingHttpTests.cs
  (V12-CWB-001 sahipliğinde) — yeni `/branding` testleri eklenir; mevcut
  testler değişmez. Bu test projesinin `.csproj`'una yeni fixture
  eklenmedi — `026-typed-settings.up.sql` zaten dahildi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx (V1-FND-001
  sahipliğinde) — yeni `ALKAROS.Settings.BusinessIdentity.Tests` projesi
  eklenir.

## In scope

1. `BusinessAccentPalette` — 8 renk, her biri `Key`/`Label`/`Hex`, gerçek
   hesaplanmış kontrast oranı yorum olarak: amber #9C6323 (4.97:1), bordo
   #8C2F39 (8.14:1), koyu yeşil #2F6B3A (6.39:1), lacivert #1B4D7B
   (8.77:1), petrol #1F6F6B (5.92:1), mor #5B3A6E (9.23:1), tuğla #B24A1F
   (5.40:1), antrasit #3C4550 (9.73:1). `Resolve(key)` bilinmeyen/eksik
   anahtarda `DefaultKey` ("amber")'a düşer — asla doğrulanmamış bir renk
   döndürmez.
2. `BusinessNameSetting` (`WaiterMaxActiveTablesSetting` ile aynı kalıp):
   `Key = "business.name"`, `SettingDataType.Text`, varsayılan `""` (boş
   = ayarlanmamış).
3. `BusinessAccentThemeSetting`: `Key = "business.accent_theme"`,
   `SettingDataType.Text`, varsayılan `BusinessAccentPalette.DefaultKey`;
   okurken değer paletten geçirilir (palette dışı/bozuk bir DB değeri
   sessizce varsayılana düşer).
4. `QrOrderingEndpoints.cs`'e `GET /api/v1/qr/branding` — oturum/masa
   token'ı gerektirmez (işletme adı/rengi gizli değil; sayfanın ilk
   render'ı oturum kurulmadan önce ihtiyaç duyuyor), `qr-order` rate
   limit'ini paylaşır. Yanıt: `QrBrandingResponse(string BusinessName,
   string AccentColor, bool HasLogo)` — `HasLogo` şimdilik her zaman
   `false` (logo depolama ayrı, sonraki bir görev; gerçek ve doğru bir
   değer, iskelet değil).

## Out of scope

- Logo yükleme/depolama/servis (ayrı görev — screensaver'ın
  `id=1`/bytea/magic-number desenini birebir kullanacak).
- Manager'ın adı/rengi HTTP üzerinden DEĞİŞTİRMESİ — `WaiterMaxActiveTablesSetting`
  (V1-SET-006) emsaliyle aynı: operatör şimdilik `settings.typed_settings`
  tablosunu doğrudan düzenler; HTTP/UI yüzeyi (PosTerminal'de bir
  `/settings/business-identity` ekranı) ayrı, takip eden bir görev.
- CustomerWeb'in bu uç noktayı gerçekten TÜKETMESİ (üç sayfada tema/ad
  gösterimi) — ayrı bir görev; bu görev yalnız gerçek, çağrılabilir
  backend ucunu açar.
- Mevcut üretim varsayılanı `#B5772F`'nin kendisinin düzeltilmesi — bu,
  CustomerWeb'in ayarlanmamış durumdaki varsayılan görünümünü değiştirir
  (ayrı, görünürlüğü olan bir karar; bu görev yalnız YENİ, seçilebilir bir
  palet sunuyor, mevcut varsayılanı geriye dönük değiştirmiyor).

## Dependencies

- None

## Acceptance evidence

- Docker tabanlı gerçek Postgres + gerçek Host testi (`docker compose -f
  compose.yaml -f compose.test.yaml run --build --rm test` veya hedefine
  daraltılmış eşdeğeri): `ALKAROS.Settings.BusinessIdentity.Tests` (yeni)
  ve `ALKAROS.Host.Experience.QrOrdering.Tests` (güncellenmiş) yeşil.
- `GET /branding` gerçek HTTP testi: hiç ayar yapılmamışken
  `{BusinessName:"", AccentColor:"#9C6323", HasLogo:false}` döner (varsayılan
  palet); bir isim/renk ayarlandıktan sonra doğru değerleri döner; palet
  dışı bozuk bir DB değeri varsayılana düşer (gerçek senaryo, revert-and-confirm
  değil — doğrudan bozuk veri ekleyip okunuşu doğrulanır).
- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata (Docker imajı
  içinde, `deploy/docker/Dockerfile`'ın `test` aşaması).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- Semih'in elle deneyebileceği senaryo: `settings.typed_settings`
  tablosuna `business.name`/`business.accent_theme` için elle bir satır
  ekle (veya `SetValueAsync` ile), `GET /api/v1/qr/branding`'i çağır,
  girilen adı ve seçilen paletin gerçek hex kodunu gör.

## Handoff

- None

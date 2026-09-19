# V1-CUI-012 - PosTerminal business identity settings screen

- Task ID: V1-CUI-012
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`V1-SET-007/008/009`'un ertelediği son madde: bir yöneticinin işletme
adını, WCAG-doğrulanmış aksan rengini ve logosunu API'yi veya SQL'i hiç
bilmeden, PosTerminal'den ayarlayabilmesi. `/settings/business-identity` —
`/settings/relay`/`/settings/screensaver` ile aynı bağımsız-URL + kendi
personel girişi deseni (henüz gerçek bir back-office navigasyonuna
taşınmadı, onlarla aynı gerekçe).

`CustomerDisplayScreensaverSettings.tsx`'in kendi bilinen kısıtı ("bu
oturumda yapılan son işlemi gösterir, sunucudaki gerçek durumu değil" —
ekran koruyucunun kendi GET'i yalnızca display-principal'a açık olduğu
için) burada YOK: `GET /api/v1/qr/branding` ve `GET /api/v1/qr/logo`
(V1-SET-007/008) kasıtlı olarak public/oturumsuz, bu yüzden bu ekran
sayfa her açıldığında sunucudaki GERÇEK mevcut ad/renk/logoyu çeker ve
önceden doldurur — screensaver ekranının kısıtı burada tekrarlanmadı.

Renk seçimi serbest bir hex alanı değil, sunucunun kendi 8 renklik
`BusinessAccentPalette`'inden ("Backend akıllı, frontend aptal",
`docs/design/foundations.md` §0) — bu yüzden yeni bir salt-okunur
`GET /api/v1/management/business-identity/accent-palette` uç noktası
eklendi; istemci paleti asla kendi kopyalamıyor/uydurmuyor.

## Owned surface

- `src/Clients/PosTerminal/src/routes/BusinessIdentitySettings.tsx` (yeni)
- `src/Clients/PosTerminal/src/routes/BusinessIdentitySettings.test.tsx` (yeni)
- `src/Clients/PosTerminal/src/routes/business-identity-settings.css` (yeni)
- `evidence/V1-CUI-012/**` (yeni)
- Sınırlı ek (paylaşılan, geri-tik olmadan, mevcut sahiplikte kalır):
  src/Clients/PosTerminal/src/App.tsx — `/settings/business-identity`
  önekinin yeni ekrana yönlendirilmesi, `/settings/relay` ile aynı desende.
- Sınırlı ek (paylaşılan, geri-tik olmadan, mevcut sahiplikte kalır):
  src/Clients/PosTerminal/src/api.ts — yeni `qrBranding`/`accentPalette`/
  `getSetting`/`updateSetting`/`fetchBusinessLogo`/`uploadBusinessLogo`/
  `removeBusinessLogo` metodları; mevcut hiçbir metod değişmedi.
- Sınırlı ek (paylaşılan, geri-tik olmadan, mevcut sahiplikte kalır):
  src/Clients/PosTerminal/src/contracts.ts — yeni `QrBrandingResponse`/
  `AccentPaletteEntry`/`AccentPaletteResponse`/`SettingRecord` tipleri.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Settings/BusinessIdentityLogoEndpoints.cs
  (V1-RMD-246 sahipliğinde, V1-SET-008'in eklediği dosya) — yeni
  `GET /api/v1/management/business-identity/accent-palette` (aynı
  `settings.manage` yönetici grubunda, salt-okunur, palet listesi + varsayılan).
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Settings/BusinessIdentityLogoEndpointsTests.cs
  (V1-RMD-246 sahipliğinde) — yeni palet uç noktası testi.

## In scope

1. `GET /api/v1/management/business-identity/accent-palette` — `settings.manage`,
   8 renk + `defaultKey`.
2. Ekran: kasa ekranıyla aynı personel girişi (`api.login`/`api.session`),
   `settings.manage` yetkisi kontrolü. Yetkisiz → net bir "erişim yetkiniz
   yok" mesajı (screensaver/relay ekranlarıyla aynı desen).
3. Yetkili girişte: `GET /api/v1/qr/branding` (public, aynı zamanda
   `business.name`/`business.accent_theme`'i ilk kez okunuyorsa kayıt
   eder — sonraki adımların 404 almaması bunu garanti eder), palet listesi
   ve `GET /api/v1/qr/logo` (public) paralel çekilir; form gerçek mevcut
   değerlerle önceden doldurulur.
4. İşletme adı: metin girişi, kaydet → `GET .../settings/business.name`
   (güncel `RowVersion` için) sonra `PUT` aynı ekranda.
5. Aksan rengi: paletten radio/swatch seçimi (serbest hex yok), kaydet →
   aynı GET+PUT deseni `business.accent_theme` için.
6. Logo: gerçek mevcut logo önizlemesi (varsa), yeni dosya seç/önizle
   (PNG/JPEG/WEBP, 5 MB) yükle (`PUT`), kaldır (`DELETE`) — ekran koruyucu
   ekranının yükleme UI desenini izler, ama gerçek GET-and-prefill ile.

## Out of scope

- Rezervasyon istasyonu ekranı gibi bir kasa-içi menü bağlantısı — RelaySettings/
  ScreensaverSettings ile aynı, bağımsız URL, henüz back-office navigasyonuna
  taşınmadı.
- Palete yeni renk eklemek/çıkarmak veya varsayılanı değiştirmek — V1-SET-007/
  V1-RMD-257'nin işi, bu görev yalnızca var olan paleti sunuyor.
- İşletme adı/rengi için tarihçe (`GET .../settings/{key}/history`) gösterimi
  — API zaten var (V1-RMD-246), bu ekran şimdilik göstermiyor.

## Dependencies

- V1-SET-007
- V1-SET-008
- V1-RMD-246

## Acceptance evidence

- `pnpm --dir src/Clients/PosTerminal typecheck`, `test`, `build`: hepsi
  sıfır çıkış kodu. Yeni `BusinessIdentitySettings.test.tsx`: oturumsuz
  personel girişi gösterir; `settings.manage` yetkisi yokken "erişim
  yetkiniz yok" gösterir; yetkiliyken gerçek `qrBranding`/`accentPalette`/
  `fetchBusinessLogo` çağrılarıyla formu önceden doldurur (mock fetch,
  gerçek bileşen render'ı); ad/renk kaydetme gerçek GET (RowVersion) + PUT
  sırasını izler; logo yükleme/kaldırma gerçek `FormData`/`DELETE` çağrısı
  yapar.
- Gerçek Postgres + gerçek Host'a karşı HTTP testi (yeni
  `AccentPaletteReturnsAllEightColorsAndTheRealDefaultKey` testi,
  `BusinessIdentityLogoEndpointsTests.cs`'e eklendi): 8 renk + doğru
  `defaultKey` (`BusinessAccentPalette.DefaultKey`'e dinamik referans).
- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı, 0 hata.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- Semih'in elle deneyebileceği senaryo: `/settings/business-identity`'ye
  git, yönetici girişi yap; işletme adını ve rengini değiştir, logo yükle;
  QR sayfasını (Menu) yeniden yükleyip üçünün de yansıdığını gör; ekranı
  kapatıp tekrar açtığında formun (bu kez sunucudan gerçekten okunan)
  aynı değerlerle dolu geldiğini gör.

## Handoff

- None

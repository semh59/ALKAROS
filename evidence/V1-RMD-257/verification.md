# V1-RMD-257 - Verification (gerçek kanıt)

## Değişiklik

`BusinessAccentPalette.DefaultKey`: `"amber"` (`#9C6323`) → `"lacivert"`
(`#1B4D7B`) — Cashier (`cashier-app.css`), WaiterPwa (`waiter-app.css`) ve
PosTerminal'in (`tokens.css`, `--ds-color-brand`) üçünün de zaten kullandığı
tam olarak aynı hex. Palet listesinin kendisi değişmedi — "amber" hâlâ
seçilebilir bir üye, yalnızca artık varsayılan değil.

CustomerWeb'in üç CSS dosyasındaki sabit `--cw-accent` varsayılanı da
`#b5772f`'den `#1b4d7b`'ye güncellendi — aksi hâlde `loadBranding()`'in
(V1-SET-009) fetch'i tamamlanana kadar sayfa bir an eski kahverengiyle
açılıp sonra laciverte geçerdi.

## Gerçek build/test sonuçları (Docker + gerçek Postgres)

```text
$ dotnet build ALKAROS.slnx --configuration Release --no-restore
Build succeeded. 0 Warning(s), 0 Error(s)

$ dotnet build ALKAROS.slnx --configuration Debug --no-restore
Build succeeded. 0 Warning(s), 0 Error(s)

$ dotnet test tests/Modules/Settings/BusinessIdentity/ALKAROS.Settings.BusinessIdentity.Tests.csproj
Passed! - Failed: 0, Passed: 24, Skipped: 0, Total: 24
```

(Tüm testler `BusinessAccentPalette.DefaultKey`'e dinamik referans veriyordu
— hiçbiri `"amber"`'ı hardcoded beklemiyordu — bu yüzden değişiklik
regresyonsuz geçti; bu da ayrıca doğrulandı: kod tabanında `"amber"` sabit
literal'i yalnızca palet tanımının kendisinde kalıyor.)

```text
$ dotnet test tests/Host/Experience/QrOrdering/ALKAROS.Host.Experience.QrOrdering.Tests.csproj
Failed! - Failed: 2, Passed: 28, Skipped: 0, Total: 30
```

Kalan 2 başarısızlık (`AValidSessionListsAvailableProductsOnTheMenu`,
`AnUnavailableProductIsHiddenFromTheMenu`) V1-SET-007/008'in kendi
verification.md'lerinde `git stash` ile zaten bu görevlerden bağımsız/
öncedendi olduğu kanıtlanmış aynı iki test — bu görev onlara dokunmadı.
`BrandingIsReachableWithNoSessionAndDefaultsToTheUnsetPaletteColor` dahil
28 test yeşil — gerçek bir HTTP çağrısıyla yeni varsayılanın
`GET /api/v1/qr/branding` üzerinden de doğru yansıdığı doğrulandı.

```text
$ python -m pytest tests/Apps/CustomerWeb -q
16 passed
```

## Gerçek Chromium (Playwright) ile doğrulama

Aynı stub sunucu + gerçek statik dosyalar pipeline'ı (V1-SET-009'da
kurulan), bu kez hiç ayar yapılmamış bir kurulumu simüle ederek
(`businessName:"", accentColor:"#1B4D7B", hasLogo:false` — artık gerçek
backend varsayılanının kendisi):

`evidence/V1-RMD-257/menu-default-now-navy.png`: aktif "Tümü" sekmesi ve
sepet çubuğu artık lacivert (#1B4D7B) — eski kahverengi/amber değil, ve
Cashier/WaiterPwa/PosTerminal'in gerçek `--color-brand`/`--ds-color-brand`
değeriyle birebir aynı.

## Gate'ler

```text
$ python tools/plan-audit/plan_audit_tool.py validate
Validation errors: 1 (C54_APPLICATION_ADMISSION_V3_FINAL_MISSING — öncedendi,
ilgisiz, değişmedi)
Validation warnings: 0

$ python tools/consistency-audit/consistency_audit.py
consistency-audit: clean
```

## Kapanış diff kontrolü

`git status --short`, Owned surface (tamamı "Sınırlı ek") ile birebir
eşleşiyor:

```text
 M src/Apps/CustomerWeb/Bill/wwwroot/bill.css
 M src/Apps/CustomerWeb/Menu/wwwroot/menu-app.css
 M src/Apps/CustomerWeb/OrderEntry/wwwroot/order-entry.css
 M src/Modules/Settings/BusinessIdentity/BusinessAccentPalette.cs
?? evidence/V1-RMD-257/
?? plan/v1/remediation/V1-RMD-257-business-accent-default-matches-brand-navy.md
```

## Manuel senaryo (Semih'in elle deneyebileceği)

Hiç `business.accent_theme` ayarlanmamış bir kurulumda
`GET /api/v1/qr/branding` çağrısının artık `accentColor:"#1B4D7B"`
döndürdüğü ve QR sayfalarının Cashier/WaiterPwa/PosTerminal ile aynı
laciverti gösterdiği yukarıdaki gerçek HTTP testi ve Chromium ekran
görüntüsüyle doğrulandı.

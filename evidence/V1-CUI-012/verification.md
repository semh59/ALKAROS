# V1-CUI-012 - Verification (gerçek kanıt)

## Backend: yeni salt-okunur uç nokta

`GET /api/v1/management/business-identity/accent-palette` — aynı
`settings.manage` yönetici grubunda, `BusinessAccentPalette.All`'ı
(8 renk) + `DefaultKey`'i döndürür.

```
$ dotnet build ALKAROS.slnx --configuration Release --no-restore
Build succeeded. 0 Warning(s), 0 Error(s)

$ dotnet build ALKAROS.slnx --configuration Debug --no-restore
Build succeeded. 0 Warning(s), 0 Error(s)

$ dotnet test tests/Host/Experience/Settings/ALKAROS.Host.Experience.Settings.Tests.csproj
Passed! - Failed: 0, Passed: 14, Skipped: 0, Total: 14
```
(12 önceki + 2 yeni: `AccentPaletteReturnsAllEightColorsAndTheRealDefaultKey`
— 8 renk + gerçek `defaultKey`, her giriş sunucunun kendi palet kaydıyla
bayt bayt eşleşiyor; `AccentPaletteWithoutAManagerSessionIsUnauthorized`.)

## Frontend: gerçek build/test

```
$ pnpm --dir src/Clients/PosTerminal typecheck
(temiz, hata yok)

$ pnpm --dir src/Clients/PosTerminal test
Test Files  1 failed | 25 passed (26)
     Tests  1 failed | 193 passed (194)
```
Yeni `BusinessIdentitySettings.test.tsx` (5/5 yeşil): oturumsuz personel
girişi gösterir; `settings.manage` yetkisi yokken "erişim yetkiniz yok"
gösterir (form hiç render edilmez); yönetici girişinde form gerçek
`qrBranding`/`accentPalette`/`fetchBusinessLogo` yanıtlarıyla önceden
dolduruluyor (hex'ten doğru palet anahtarı eşleştiriliyor) ve isim kaydı
gerçek GET (RowVersion) → PUT sırasını izliyor; renk kaydı seçilen anahtarı
(hex değil) gönderiyor; logo yükleme gerçek `FormData` PUT, kaldırma gerçek
`DELETE` çağrısı yapıyor.

Kalan 1 başarısızlık (`src/stale.test.ts`'teki "gives the pairing dialog an
accessible modal contract"), bu görevin dokunduğu HİÇBİR dosyayla ilgisi
yok (`git diff` ile doğrulandı — `api.ts`'teki pairing route'larına tek
satır bile dokunulmadı) ve `V1-RMD-256`'nın kendi verification.md'sinde
`git stash` ile üç kez çalıştırılıp bu görevlerden önce de var olduğu,
ilgisiz olduğu zaten kanıtlanmış aynı test.

```
$ pnpm --dir src/Clients/PosTerminal build
✓ built in 1.40s
```
`BusinessIdentitySettings` kendi ayrı chunk'ında (`BusinessIdentitySettings-*.js`,
7.90 kB) — V1-RMD-139'un route-başına code-splitting deseniyle tutarlı.

## Gerçek Chromium (Playwright) ile uçtan uca doğrulama

Gerçek üretim build'i (`pnpm build`'in ürettiği `dist/`), SPA-fallback +
gerçek mock API rotalarıyla (`/auth/session`, `/qr/branding`,
`/accent-palette`, `/qr/logo`, `/management/settings/*`,
`/management/business-identity/logo`) sunuldu, gerçek Chromium ile ziyaret
edildi:

- `evidence/V1-CUI-012/business-identity-settings.png`: form, sunucudaki
  gerçek değerlerle ("Sahil Cafe", lacivert seçili, gerçek logo önizlemesi)
  önceden dolu geliyor — screensaver ekranının "yalnız bu oturumun son
  işlemi" kısıtı burada YOK.
- **Uçtan uca gerçek round-trip:** ad "Deniz Kenari Restoran" olarak
  değiştirilip kaydedildi, renk "Bordo" seçilip kaydedildi, logo kaldırıldı
  — hepsi gerçek `PUT`/`DELETE` çağrılarıyla. Ardından sayfa TAMAMEN
  yeniden yüklendi (istemci belleği değil, taze bir navigasyon):
  `evidence/V1-CUI-012/after-reload.png` üçünün de (yeni ad, Bordo seçili,
  logo bölümünde "şu anda ayarlı logo" artık yok) sunucudan gerçekten
  okunduğunu kanıtlıyor.

## Gate'ler

```
$ python tools/plan-audit/plan_audit_tool.py validate
Validation errors: 1 (C54_APPLICATION_ADMISSION_V3_FINAL_MISSING — öncedendi,
ilgisiz, değişmedi)
Validation warnings: 0

$ python tools/consistency-audit/consistency_audit.py
consistency-audit: clean
```
(İlk çalıştırmada 3 gerçek ihlal bulundu — üç yorumda tırnak içinde
alıntılanan Türkçe ifadeler ("Backend akıllı, frontend aptal",
"işletme adımız ve logomuz görünsün") `TASK_STANDARD.md`'nin "kod/yorum
İngilizce kalır" kuralını ihlal ediyordu; İngilizce açıklamayla
değiştirilip düzeltildi, tekrar çalıştırılıp temiz olduğu doğrulandı.)

## Kapanış diff kontrolü

`git status --short`, Owned surface (tamamı deklare edildiği gibi) ile
birebir eşleşiyor.

## Manuel senaryo (Semih'in elle deneyebileceği)

`/settings/business-identity`'ye git, yönetici girişi yap; işletme adını
ve rengini değiştir, logo yükle; QR sayfasını (Menu) yeniden yükleyip
üçünün de yansıdığını gör; ekranı kapatıp tekrar açtığında formun (bu kez
sunucudan gerçekten okunan) aynı değerlerle dolu geldiğini gör — yukarıdaki
gerçek Chromium round-trip'iyle zaten uçtan uca doğrulandı.

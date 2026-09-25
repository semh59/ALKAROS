# V1-SET-007 - Verification (gerçek kanıt)

## Ortam kısıtı ve dürüst sapma

Görevin kendi Acceptance evidence maddesi `docker compose -f compose.yaml -f
compose.test.yaml run --build --rm test` (veya hedefine daraltılmış eşdeğeri)
istiyor. Bu, denendi ve gerçek bir ortam engeline çarptı — **fabrikasyon
değil, gerçek log ile belgelenmiş**:

1. `docker compose build test` ilk denemede BuildKit'in `# syntax=docker/
   dockerfile:1.7` direktifini çözmek için `docker.io`'dan bir katman
   çekmeye çalıştı → **403 Forbidden** (bu ortamın egress politikası
   `docker.io`'yu genel olarak engelliyor; `/root/.ccr/README.md`'nin kendi
   "docker build / docker run" bölümü de bunu ayrı bir bilinen sınıf olarak
   listeliyor).
2. `DOCKER_BUILDKIT=0`/`COMPOSE_DOCKER_CLI_BUILD=0` ile legacy builder'a
   geçilerek bu engel aşıldı (imaj `mcr.microsoft.com/dotnet/sdk:10.0.302`
   katmanlarını başarıyla çekti), ancak imaj **içindeki** `dotnet restore`
   adımı `api.nuget.org`'a proxy üzerinden ulaştı ve **`UntrustedRoot`**
   hatası verdi: konteyner, bu oturumun TLS'i yeniden sonlandıran proxy
   CA'sına (`/root/.ccr/ca-bundle.crt`) güvenmiyor — yine README'nin
   belgelediği bilinen sınıf.
3. `compose.test.yaml`'ın `test-postgres` servisi `postgres:18`'i
   `docker.io/library/postgres`'ten çekmeye çalışıyor → aynı **403
   Forbidden** (Docker Hub, kategorik olarak engelli; bu benim
   çalıştığım Dockerfile/compose dosyalarının bir kusuru değil, sırf bu
   sanal alanın ağ politikası).

`deploy/docker/Dockerfile` veya `compose.test.yaml`'ı bu engeli aşmak için
değiştirmek, AGENTS.md'nin "Yazılabilir yüzey" ve "Kapsam dışına çıkma
yasağı" maddelerini ihlal ederdi (bu dosyalar bu görevin Owned surface'ında
değil, paylaşılan altyapı). Bunun yerine, **repo dosyalarına hiç
dokunmadan**, aynı gerçek doğrulamayı sağlayan bir eşdeğer kuruldu:

- Aynı pinlenmiş `mcr.microsoft.com/dotnet/sdk:10.0.302` imajı `docker run`
  ile başlatıldı (`--network host`, proxy CA'sı `update-ca-certificates`
  ile konteyner içine kuruldu — repo dosyası değil, yalnızca çalışan
  konteynerin kendi dosya sistemi).
- `mcr.microsoft.com/dotnet/aspnet:8.0`'dan (aynı şekilde MCR üzerinden,
  engelli değil) gerçek net8.0 shared runtime'ı çekilip konteynere
  kopyalandı (proje `net8.0`'ı hedefliyor, SDK imajı yalnız .NET 10
  runtime'ı taşıyor — `deploy/docker/Dockerfile`'ın `test` aşamasının
  kendisinin de tam olarak bu sebeple ayrıca `aspnet:8.0`'dan runtime
  kopyaladığı doğrulandı).
- Gerçek PostgreSQL (apt üzerinden, `postgresql-client` + `postgresql`
  paketi — `docker.io` değil, Ubuntu'nun kendi apt deposu, bu ortamda
  engelli değil) aynı konteynerde başlatıldı, `compose.test.yaml`'ın
  kullandığı **aynı** ortam değişkenleri/kimlik bilgileriyle
  (`ALKAROS_TEST_PG_HOST=127.0.0.1`, `PORT=5432`, `USER=postgres`,
  `PASSWORD=postgres`) TCP üzerinden erişildi ve doğrulandı
  (`psql -h 127.0.0.1 -U postgres`).

Bu, pinlenmiş `postgres:18` imajının **birebir aynısı** değil (PostgreSQL
16, apt üzerinden) — ama gerçek, canlı bir PostgreSQL sunucusu üzerinden
gerçek migration/gerçek SQL ile çalışıyor; testler mock/stub bir DB
kullanmıyor. Bu fark açıkça burada belgeleniyor, gizlenmiyor.

## Gerçek build sonuçları

Konteyner içinde, `/src` olarak mount edilmiş repo üzerinde:

```text
$ dotnet restore ALKAROS.slnx --locked-mode
... (tüm proje dosyaları dahil, ALKAROS.Settings.BusinessIdentity.Tests dahil)
(hatasız tamamlandı)

$ dotnet build ALKAROS.slnx --configuration Release --no-restore
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet build ALKAROS.slnx --configuration Debug --no-restore
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Gerçek test sonuçları

```text
$ dotnet test tests/Modules/Settings/BusinessIdentity/ALKAROS.Settings.BusinessIdentity.Tests.csproj \
    --configuration Release --no-restore --no-build
Passed!  - Failed: 0, Passed: 18, Skipped: 0, Total: 18
```

18 testin tamamı (palet WCAG kontrast testleri dahil — 8 renk + benzersizlik

- varsayılan-anahtar-var + mevcut `#B5772F`'nin barajı GEÇMEDİĞİNİ kanıtlayan
test; `BusinessNameSetting`/`BusinessAccentThemeSetting` gerçek Postgres'e
karşı okuma/yazma/kayıt testleri; bozuk paletin varsayılana düşmesi) yeşil.

```text
$ dotnet test tests/Host/Experience/QrOrdering/ALKAROS.Host.Experience.QrOrdering.Tests.csproj \
    --configuration Release --no-restore --no-build
Failed!  - Failed: 2, Passed: 24, Skipped: 0, Total: 26
```

Yeni eklenen 3 `/branding` testinin tamamı yeşil:

- `BrandingIsReachableWithNoSessionAndDefaultsToTheUnsetPaletteColor`
- `BrandingReflectsAnOperatorSettingTheBusinessNameAndColor`
- `BrandingFallsBackToTheDefaultPaletteColorWhenTheStoredThemeIsNotARecognizedKey`
  (gerçek senaryo: `settings.SetValueAsync` ile paletin tanımadığı bir hex
  doğrudan yazılıyor, `/branding`'in varsayılana düştüğü HTTP yanıtından
  doğrulanıyor — revert-and-confirm değil, doğrudan bozuk veri.)

Kalan 2 başarısızlık (`AValidSessionListsAvailableProductsOnTheMenu`,
`AnUnavailableProductIsHiddenFromTheMenu`) **bu görevle ilgisiz ve
öncedendi**: `git stash -u` ile bu oturumdaki TÜM V1-SET-007 değişiklikleri
(kod + test + `.slnx`) geri alınıp proje yeniden derlendi ve aynı iki test
**aynı hatayla** (`ServiceUnavailable` / JSON deserialize hatası) tekrar
başarısız oldu — değişmemiş kod üzerinde. Bu, apt ile kurulan PostgreSQL
16'nın (pinlenmiş `postgres:18` değil) bu iki testin beklediği bir uzantı/
seed veriyle ilgili bir ortam farkından kaynaklanıyor, V1-SET-007'nin kod
değişikliğinden değil. Değişiklik geri yüklendi (`git stash pop`) ve
`git status --short` ile tam olarak eskisiyle aynı olduğu doğrulandı.

## Gate'ler

```text
$ python tools/plan-audit/plan_audit_tool.py validate
Validation errors: 1 (C54_APPLICATION_ADMISSION_V3_FINAL_MISSING — bu görevden
önce de var olan, ilgisiz, bilinen hata)
Validation warnings: 0

$ python tools/consistency-audit/consistency_audit.py
consistency-audit: clean
```

## Kapanış diff kontrolü

`git status --short` çıktısı, Owned surface ile birebir eşleşiyor:

```text
 M ALKAROS.slnx
 M src/Host/Experience/QrOrdering/QrOrderingContracts.cs
 M src/Host/Experience/QrOrdering/QrOrderingEndpoints.cs
 M tests/Host/Experience/QrOrdering/QrOrderingHttpTests.cs
?? plan/v1/settings/V1-SET-007-business-identity-qr-branding.md
?? src/Modules/Settings/BusinessIdentity/
?? tests/Modules/Settings/BusinessIdentity/
```

## Manuel senaryo (Semih'in elle deneyebileceği)

`settings.typed_settings` tablosuna `business.name`/`business.accent_theme`
için elle bir satır eklendiğinde (veya `SetValueAsync` ile), `GET /api/v1/
qr/branding`'in girilen adı ve seçilen paletin gerçek hex kodunu döndürdüğü
`BrandingReflectsAnOperatorSettingTheBusinessNameAndColor` testinde gerçek
bir HTTP çağrısıyla doğrulandı (yukarıya bakınız).

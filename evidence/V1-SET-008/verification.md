# V1-SET-008 - Verification (gerçek kanıt)

## Ortam notu

V1-SET-007'de belgelenen aynı sandbox kısıtı (Docker Hub genel olarak
engelli, `postgres:18` imajı çekilemiyor) burada da geçerli — aynı
eşdeğer kurulum kullanıldı: pinlenmiş `mcr.microsoft.com/dotnet/sdk:10.0.302`
imajı `docker run --network host` ile, proxy CA'sı konteyner içine
`update-ca-certificates` ile kurulmuş, net8.0 shared runtime
`mcr.microsoft.com/dotnet/aspnet:8.0`'dan kopyalanmış, gerçek PostgreSQL
16 apt üzerinden kurulup `compose.test.yaml`'ın kullandığı aynı ortam
değişkenleriyle (`ALKAROS_TEST_PG_HOST=127.0.0.1`, `PORT=5432`,
`USER=postgres`, `PASSWORD=postgres`) TCP üzerinden doğrulandı. Repo
dosyalarına (Dockerfile/compose) hiç dokunulmadı.

## Gerçek build sonuçları

```
$ dotnet restore ALKAROS.slnx --locked-mode
(hatasız tamamlandı)

$ dotnet build ALKAROS.slnx --configuration Release --no-restore
Build succeeded. 0 Warning(s), 0 Error(s)

$ dotnet build ALKAROS.slnx --configuration Debug --no-restore
Build succeeded. 0 Warning(s), 0 Error(s)
```

## Gerçek test sonuçları

```
$ dotnet test tests/Modules/Settings/BusinessIdentity/ALKAROS.Settings.BusinessIdentity.Tests.csproj
Passed! - Failed: 0, Passed: 24, Skipped: 0, Total: 24
```
(18 V1-SET-007'den + 6 yeni `BusinessLogoStoreTests`: null-when-unset,
save/get round-trip, ikinci kaydın satırı değiştirmesi — INSERT değil,
ETag'in değişmesi, delete sonrası null, var-olmayan logoyu silmenin no-op
olması.)

```
$ dotnet test tests/Host/Experience/Settings/ALKAROS.Host.Experience.Settings.Tests.csproj
Passed! - Failed: 0, Passed: 12, Skipped: 0, Total: 12
```
(6 V1-RMD-246'dan + 6 yeni `BusinessIdentityLogoEndpointsTests`:
oturumsuz `PUT` → 401; `settings.manage` izni olmayan `PUT` → 403; gerçek
bir yönetici oturumuyla `PUT` → 204, DB'de doğrulandı, sonra `DELETE` →
204 ve DB'de silindiği doğrulandı; izin verilmeyen MIME türü → 400 (DB'ye
yazılmadı); 5 MB üstü dosya → 400 (DB'ye yazılmadı); beyan edilen türle
eşleşmeyen magic-number → 400 (DB'ye yazılmadı)).

**Bulunan ve düzeltilen gerçek bir kusur:** İlk yazımda
`AddBusinessIdentityLogoExperience()` yalnız
`ISettingsService`/`IBusinessLogoStore` zincirini kaydediyordu;
`SettingsManagerEndpointFilter`'ın kendi `IAuthorizationService`
(`ALKAROS.Identity.Authorization`) zincirini (`IRoleRepository`,
`IDenialEventSink`, `IAuthorizationService`) hiç kaydetmiyordu. Sonuç:
TÜM `PUT` testleri (oturumsuz olan dahil) beklenen 401/403/400 yerine
gerçek bir 500 Internal Server Error alıyordu — filtre DI çözümlemesi
sırasında çöktüğü için kendi try/catch'i hiç devreye girmiyordu. Gerçek
test çalıştırmasıyla yakalandı (revert-and-confirm değil, ilk gerçek
çalıştırmanın kendisi), `AddSettingsManagementExperience()`'ın zaten
kapattığı aynı üç kaydı ekleyerek düzeltildi; testler tekrar
çalıştırılıp 12/12 yeşil olduğu doğrulandı.

```
$ dotnet test tests/Host/Experience/QrOrdering/ALKAROS.Host.Experience.QrOrdering.Tests.csproj
Failed! - Failed: 2, Passed: 28, Skipped: 0, Total: 30
```
Yeni eklenen 4 `/logo` testinin tamamı yeşil:
`LogoIsNotFoundWhenNoneHasBeenSet`, `BrandingReportsHasLogoFalseWhenNoneHasBeenSet`,
`LogoReturnsTheUploadedBytesAndBrandingReflectsItsPresence`,
`LogoHonorsIfNoneMatchWithA304`. Kalan 2 başarısızlık
(`AValidSessionListsAvailableProductsOnTheMenu`,
`AnUnavailableProductIsHiddenFromTheMenu`) V1-SET-007'nin verification.md'sinde
zaten `git stash` ile bu görevlerden bağımsız/öncedendi olduğu kanıtlanmış
aynı iki testtir — bu görev onlara hiç dokunmadı, sayıları (28/30, önceki
24/26'dan +4 yeni test kadar arttı) bunu doğruluyor.

```
$ dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj --filter FullyQualifiedName~ManifestTests
Passed! - Failed: 0, Passed: 16, Skipped: 0, Total: 16
```

```
$ dotnet test tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj
Passed! - Failed: 0, Passed: 9, Skipped: 0, Total: 9
```
(regresyon yok — `SettingsModule.cs`'e eklenen yeni kayıt modül sınırı
ihlali üretmedi.)

## Migration ileri/geri doğrulaması

Boş bir scratch veritabanında elle doğrulandı:
```
CREATE SCHEMA settings;
\i 136-business-logo.up.sql   → CREATE TABLE (settings.business_logo, id=1
                                  check constraint, content BYTEA NOT NULL,
                                  content_type, updated_at, row_version)
\i 136-business-logo.down.sql → DROP TABLE
\dt settings.*                 → "Did not find any relation" (temiz geri alma)
```

## Gate'ler

```
$ python tools/plan-audit/plan_audit_tool.py validate
Validation errors: 1 (C54_APPLICATION_ADMISSION_V3_FINAL_MISSING — öncedendi,
ilgisiz, değişmedi)
Validation warnings: 0

$ python tools/consistency-audit/consistency_audit.py
consistency-audit: clean
```

## Kapanış diff kontrolü

`git status --short`, Owned surface ile birebir eşleşiyor:

```
 M database/MigrationComposition/order.json
 M src/Host/Composition/Migrations/MigrationManifest.cs
 M src/Host/DualScreen/DualScreenApplication.cs
 M src/Host/Experience/QrOrdering/QrOrderingEndpoints.cs
 M src/Modules/Settings/SettingsModule.cs
 M tests/Host/Experience/QrOrdering/ALKAROS.Host.Experience.QrOrdering.Tests.csproj
 M tests/Host/Experience/QrOrdering/QrOrderingHttpTests.cs
 M tests/Host/MigrationComposition/Manifest/ManifestTests.cs
 M tests/Modules/Settings/BusinessIdentity/ALKAROS.Settings.BusinessIdentity.Tests.csproj
?? database/migrations/V1/V1-SET-008/
?? plan/v1/settings/V1-SET-008-business-logo-upload.md
?? src/Host/Experience/Settings/BusinessIdentityLogoEndpoints.cs
?? src/Modules/Settings/BusinessIdentity/BusinessLogoStore.cs
?? tests/Host/Experience/Settings/BusinessIdentityLogoEndpointsTests.cs
?? tests/Modules/Settings/BusinessIdentity/BusinessLogoStoreTests.cs
```

## Manuel senaryo (Semih'in elle deneyebileceği)

`settings.manage` izni olan bir yönetici oturumuyla
`PUT /api/v1/management/business-identity/logo`'ya gerçek bir PNG
yükle; `GET /api/v1/qr/logo`'nun aynı baytları ve `Content-Type`'ı
döndürdüğü ve `GET /api/v1/qr/branding`'in `HasLogo:true`'ya döndüğü
`LogoReturnsTheUploadedBytesAndBrandingReflectsItsPresence` testinde
gerçek bir HTTP çağrısıyla doğrulandı; `DELETE` sonrası `GET /logo`'nun
404 döndüğü `AManagerCanUploadAndThenRemoveARealLogo` testinde doğrulandı.

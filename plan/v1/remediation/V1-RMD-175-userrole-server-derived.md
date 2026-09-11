# V1-RMD-175 - #userRole artık sunucudan gelen gerçek rol adını gösteriyor

- Task ID: V1-RMD-175
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin Frontend bölümündeki Low
bulgulardan birini kapatır: "`#userRole` hiç güncellenmiyor". Garson
ekranındaki üst bar, giriş yapan kullanıcının rolünü göstermesi gereken bir
`#userRole` elemanına sahip, ama HTML'deki statik "Garson" varsayılanından
hiç değişmiyordu — çünkü ne `POST /api/v1/auth/login` ne de
`GET /api/v1/auth/session` yanıtları hiçbir zaman kullanıcının rolünü
taşımıyordu. Bir vardiya amiri kendi cihazına giriş yapsa bile ekranı hâlâ
"Garson" yazıyordu.

Düzeltme: her iki uç nokta da artık `roleName` alanı döndürüyor.

- `POST /api/v1/auth/login`: offline yetki bütçesi için zaten yapılan
  `roles.GetRoleIdsForUserAsync` + `roles.GetByIdAsync` çağrısının sonucu
  (`Role?`) hoisted edilip yanıt gövdesine `roleName = loginRole?.Name`
  olarak eklendi — ekstra bir DB sorgusu gerekmedi.
- `GET /api/v1/auth/session`: aynı `IRoleRepository` çağrı çifti
  (`GetRoleIdsForUserAsync` + `GetByIdAsync`) yeni eklendi, yanıt gövdesine
  `roleName = role?.Name` eklendi. Bu, comp/void onay uç noktalarının
  "isteği yapanın rolünü" çözmek için zaten kullandığı yöntemle aynı
  (kullanıcının ilk atanmış rolünün görünen adı).
- `waiter-app.js`'nin `applyUser(user)` fonksiyonu artık
  `el.userRole.textContent = (user && user.roleName) || 'Garson';` yazıyor
  — rol yoksa (nadiren, pratikte olmamalı) statik "Garson" varsayılanına
  düşüyor, hiç kırılmıyor.

Bir kullanıcının rolsüz olması login'i başarısız kılmaz (offline bütçe
mantığındaki aynı kural burada da geçerli); `roleName` böyle bir durumda
`null` olur ve istemci varsayılana düşer.

## Owned surface

- `plan/v1/remediation/V1-RMD-175-userrole-server-derived.md` (yeni)
- Sınırlı ek:
  - src/Host/DualScreen/DualScreenApplication.Endpoints.cs (Host sahipliğinde)
    — `/auth/login` ve `/auth/session` uç noktalarının yanıt gövdeleri.
  - src/Clients/WaiterPwa/wwwroot/waiter-app.js (V1-WTR-010 sahipliğinde)
    — `applyUser()`.
  - tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs
    (Host test sahipliğinde) — `SeedUserRoleAsync` yardımcı fonksiyonu,
    mevcut izolasyon testine `roleName` alanının varlığını doğrulayan bir
    satır, ve yeni `LoginAndSessionBothReturnTheSignedInUsersRoleName` testi.

## Out of scope

Frontend'in geri kalan Low bulguları (ölü CSS, katalog vardiya boyunca
yenilenmiyor, `nextCursor` yok sayılıyor, çift dokunma korumasız
onayla/reddet, menü her açılışta klavyeyi açıyor, canlı bölge eksikleri) —
ayrı görev/görevler. `/comp` + `/transfer-server` istemcisi de ayrı.

## Dependencies

- V1-RMD-174

## Acceptance evidence

- `dotnet build src/Host/ALKAROS.Host.csproj -c Debug` → 0 uyarı, 0 hata.
- `node --check src/Clients/WaiterPwa/wwwroot/waiter-app.js` → temiz.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test
  tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj -c Debug
  --filter "FullyQualifiedName~DualScreenAuthorizationHttpTests"`
  → 5/5 yeşil. Yeni `LoginAndSessionBothReturnTheSignedInUsersRoleName`
  testi kullanıcıya gerçek bir rol ("Vardiya Amiri") atayıp hem login hem
  session yanıtlarında bu ismin birebir döndüğünü doğruluyor (boş-değer
  kontrolü değil, gerçek bir değerin ilk uçtan son uca gittiğinin kanıtı).
  Mevcut `LoginCookieAuthorizationDenialDisplayIsolationAndRevocationAreEnforcedOverHttp`
  testine de `roleName` anahtarının login gövdesinde her zaman var
  olduğunu doğrulayan bir satır eklendi (bu testin kullanıcısı kasıtlı
  olarak rolsüz — `null` değerin de sorunsuz taşındığını gösteriyor).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.)

## Handoff

- None

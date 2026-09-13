# V1-RMD-198 - V1-RMD-189'un FK'sı, kendi test projesini kırmıştı

- Task ID: V1-RMD-198
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

V1-RMD-197'nin proje-bazlı coverage parçalaması, ilk gerçek CI koşusunda
tam olarak işe yaradı: 5 dakika içinde net bir gerçek test hatası
gösterdi (önceki 20 dakikalık OOM sessizliğinin aksine).
`ALKAROS.Host.Experience.Authorization.Tests` üç testte
`23503: insert or update on table "authorization_grants" violates
foreign key constraint "fk_authorization_grants_requester"` ile
başarısız oldu.

Kök neden: V1-RMD-189, `identity.authorization_grants`/
`authorization_delegations`/`behavioural_tightenings`'e gerçek kullanıcı
FK'ları ekledi — ama bu test projesinin kendi seed yardımcıları
(`SeedPendingGrantAsync`, `SeedActiveDelegationAsync`,
`SeedOpenTighteningAsync`), `requester_user_id`/`grantee_user_id`/
`delegator_user_id`/`user_id` için hiçbir zaman gerçek bir
`identity.users` satırına ihtiyaç duymamış, doğrudan `Guid.NewGuid()`
kullanmışlardı. V1-RMD-189'un kendi doğrulaması sırasında bu proje HİÇ
çalıştırılmamıştı — `tests/Modules/Identity/Authorization` (198/198) ve
`tests/Host/MigrationComposition` (135/135) çalıştırılmıştı, ama
`tests/Host/Experience/Authorization` ayrı bir projeydi ve gözden
kaçmıştı.

Aynı sınıftan başka bir regresyon olup olmadığı, bu dört tabloya
(`authorization_grants/delegations/behavioural_tightenings/policies`)
ve V1-RMD-191'in FK'ladığı iki billing tablosuna (`bill_adjustments/
bill_allocations`) doğrudan `INSERT` yapan HER test dosyası taranarak
doğrulandı: kalan sekiz dosyanın hepsi ya (a) yeni migration'ı (107/108)
hiç bağlamıyor — FK o şemada hiç yok, ya da (b) zaten gerçek seed
edilmiş kullanıcı id'leri kullanıyor (`SeedCashierSessionAsync`'ten
gelen id'ler gibi). Tek gerçek regresyon buydu.

## Owned surface

- `plan/v1/remediation/V1-RMD-198-authorization-decision-tests-fk-regression.md` (yeni)
- Sınırlı ek:
  - tests/Host/Experience/Authorization/AuthorizationDecisionHttpTests.cs
    (Host test sahipliğinde) — `AuthorizationDecisionTestDatabase`'e yeni
    `SeedBareUserAsync()` yardımcısı eklendi (`SeedManagerSessionAsync`'in
    kendi `identity.users` INSERT'inin oturumsuz, rolsüz hâli);
    `SeedPendingGrantAsync`/`SeedActiveDelegationAsync`/
    `SeedOpenTighteningAsync` artık `Guid.NewGuid()` yerine bunu
    kullanıyor.

## Out of scope

- Diğer sekiz dosya: tarandı, gerçek bir regresyon bulunmadı (yukarıdaki
  Goal bölümü) — değişiklik gerekmedi.

## Dependencies

- V1-RMD-189
- V1-RMD-197

## Acceptance evidence

- `dotnet build tests/Host/Experience/Authorization/*.csproj -c Debug` →
  0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet
  test tests/Host/Experience/Authorization/*.csproj -c Debug` →
  düzeltmeden önce CI'da gerçek 3/5 başarısızlık (FK ihlali); düzeltmeden
  sonra yerel olarak **5/5 yeşil**.
- Kalan sekiz dosya elle tarandı (`grep` + her birinin kendi `.csproj`'ı):
  `PostgresAdjustmentsTests.cs`/`PostgresSplitDesignTests.cs`/
  `PostgresTableTransferTests.cs`/`PostgresTableMergeTests.cs` → 107/108
  hiç bağlanmamış; `OrderManagementCompTestDatabase.cs` → parametre
  olarak gelen id'ler zaten `SeedCashierSessionAsync`'ten; Identity
  Authorization projesindeki üçü (`Policies`/`Delegations`/
  `Behavioural`) → V1-RMD-189'un kendi doğrulamasında zaten 198/198
  yeşildi, değişmedi.

## Handoff

- None

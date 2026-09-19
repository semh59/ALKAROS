# V1-RMD-236 - Require real supervisor authorization for cash-session close override

- Task ID: V1-RMD-236
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`POST .../cash-sessions/{id}/close` (`V13-CSH-004`) only checks that the
caller has a valid cashier session (`RequireCashierAsync`). When the
request body sets `IsSupervisorOverride: true`, the endpoint skips the
50 TL variance-tolerance check (`CashSessionPolicy.ValidateCanCloseSession`)
entirely, requiring only a non-empty `OverrideReason` string — no check
that the caller actually holds supervisor-level authority. A bağımsız bir
denetim ajanı (2026-09-18, tüm proje kod denetimi, Billing/Payments/Cash
alanı) bunu tespit etti: herhangi bir sıradan kasiyer, kendi API isteğinde
bu bayrağı `true` yapıp rastgele bir sebep metni yazarak sınırsız tutarda
kasa açığını/fazlasını gerçek bir amir onayı olmadan kapatabiliyor. Bu,
codebase'in kendi yerleşik desenine (`bills.void`/`bills.comp`/
`bills.discount` — supervisor-tier eskalasyonlar) aykırı.

## Owned surface

- `database/migrations/V1/V1-RMD-236/**`
- `evidence/V1-RMD-236/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Catalog/ApplicationPermissions.cs
  (V1-IAM-017 sahipliğinde) — yalnız `cash.session.override` (yeni bir
  supervisor-tier eskalasyon) eklendi, `SupervisorEscalations`e katıldı.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/Catalog/ApplicationPermissionsTests.cs
  (V1-IAM-017 sahipliğinde) — yeni katalog sayısı (18) ve yeni kodun
  supervisor/manager'da olup cashier/waiter'da olmadığını doğrulayan
  assertion'lar eklendi.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.CashSession.cs
  (V13-CSH-004 sahipliğinde kalır) — yalnız `/close` endpoint'i,
  `IsSupervisorOverride=true` iken `IAuthorizationService.AuthorizeAsync`
  ile `cash.session.override` kontrolü eklenir; diğer tüm endpoint'ler ve
  normal (override'sız) kapanış davranışı değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/CashSession/CashSessionHttpTests.cs
  (V13-CSH-004 sahipliğinde kalır) — yeni bir `SeedCashierSessionWithPermissionsAsync`
  yardımcı metodu eklenir; mevcut `ClosingWithADifferenceOverTheToleranceRequiresSupervisorOverride`
  testi GÜNCELLENİR (override adımı artık `cash.session.override` izinli bir
  oturum kullanır — bu test şu ana kadar tam olarak bu güvenlik açığını
  sergiliyordu, düzeltmeden sonra eski hâliyle kırılırdı); ayrıca izinsiz bir
  kasiyerin override denemesinin 403 aldığını doğrulayan yeni bir test eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs — yeni migration
  pozisyonunun (129) kaydı (V13-CSH-001 emsaliyle aynı desen); ayrıca
  `order.json`'daki bayat `phaseBRange.max` (112) gerçek değere (129)
  düzeltilir (bağımsız denetimde bulunan, ayrı bir küçük düzeltme).

## In scope

- Yeni izin kodu `cash.session.override`, yalnız `supervisor` ve
  `manager` rollerine (bkz. `SupervisorEscalations`) verilir — `cashier`/
  `waiter` outright sahip değildir (mevcut eskalasyon akışıyla, V1-IAM-019,
  bir kasiyer isterse bunu talep edebilir).
- `/close` endpoint'i: yalnız `IsSupervisorOverride=true` iken bu izni
  kontrol eder; `false` (normal, tolerans dahilinde kapanış) davranışı
  HİÇ değişmez.

## Out of scope

- `CashSessionPolicy.ValidateCanCloseSession`'ın kendi tolerans mantığını
  (50 TL eşiği) değiştirmek.
- Grant-request (V1-IAM-019) UI akışının kendisi — mevcut, test edilmiş
  mekanizma zaten `bills.void`/`comp`/`discount` için çalışıyor, bu görev
  yalnız aynı mekanizmayı `cash.session.override`'a bağlıyor.

## Dependencies

- V13-CSH-004
- V1-IAM-017

## Acceptance evidence

- Gerçek Postgres + gerçek Host'a karşı HTTP testi: `cash.session.override`
  izni olmayan bir kasiyer, `IsSupervisorOverride: true` ile `/close`
  çağırdığında 403 alır (kasa KAPANMAZ). Aynı izne sahip bir supervisor/
  manager başarıyla kapatabilir. Override olmadan (tolerans dahilinde)
  normal kapanış hâlâ herhangi bir kasiyer için çalışır (regresyon yok).
- `dotnet build ALKAROS.slnx -c Debug` → 0 uyarı, 0 hata.
- `dotnet test` (Identity Authorization Catalog testleri + CashSession HTTP
  testleri) → yeşil.
- Migration 129 boş bir veritabanında ileri/geri (up/down) denenir.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None

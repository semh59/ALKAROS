# V1-IAM-025 - Authorization wave hardening and requester-side wiring

- Task ID: V1-IAM-025
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: implementation
- Surface state: Existing

## Goal

`V1-IAM-016..024` dalgasını denetleyen
`docs/engineering/authz-wave-remediation-plan.md` Phase 3 ve 5'in kapsamını
tamamlar (Phase 2, D1-D3/D7/D8, ayrı `fix(v1-iam)` commit'i olarak zaten
kapatıldı): (a) üç migration (050/051/052) ile şema sağlamlaştırması (A1
offline bütçe yeniden-ihraç FK çökmesi, D4 delegation revoke actor'ı, D5
davranışsal oran sorgusu indeksi); (b) inşa edilmiş ama hiçbir canlı istek
yoluna bağlanmamış yetkilendirme motorunun istemci tarafını, gerçekten var
olan iki yolda bağlamak: login'de offline bütçe ihracı (C2) ve reconnect'te
reconciliation (C3); (c) `workspace.tsx` `/authorization` rota kablolaması
testi (D6). Motor bileşenleri `V1-IAM-018`..`V1-IAM-023` ile teslim edildi ve
DI-kayıtlı + birim/entegrasyon test edildi.

**Kapsam daraltıldı (implementasyon sırasında bulunan gerçek engel):** C1
(grant-class bir mutasyon endpoint'ine `RequestAsync` dalı eklemek), C4
(`AllowedCommands` = tutulan ∪ ulaşılabilir istek) ve C5 (davranışsal kapı +
devir çözücünün gerçek HTTP yolunda tetiklendiğini kanıtlayan entegrasyon
testi) planın varsaydığı "en az bir grant-class mutasyon endpoint'i zaten var,
yalnız bir dal eklenecek" öncülüne dayanıyordu. Kod taraması bunun yanlış
olduğunu gösterdi: `bills.void` / `bills.comp` / `bills.discount` — modelin
(§3) TEK grant-class permission ailesi — için `src/Host/Experience/**`
altında hiçbir endpoint yok; `Billing.Adjustments` domain modülü
(`AdjustmentCalculator`, `BillAdjustment`, `IBillAdjustmentRepository`) yalnız
indirim/hizmet bedeli/kuver/bahşiş modelliyor, void/comp'u hiç modellemiyor.
Bu, bağımsız denetimin **B1** bulgusuyla aynı boşluk (adjustments'ın
Experience yüzeyi yok) — C1/C4/C5'i "yalnız kablolama" olarak tamamlamak,
gerçekte yeni bir para-dokunan iş özelliği (void/comp domain modeli +
endpoint) inşa etmeyi gerektirir; bu görevin "yeni iş kuralı yok" sınırını
aşar ve kendi planlaması + Semih onayını hak eder. C1/C4/C5, `V1-IAM-026`'ya
devredildi (bkz. Handoff).

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-025-authorization-wave-hardening-and-requester-wiring.md`
- `database/migrations/V1/V1-IAM-025/**`
- `database/MigrationComposition/order.json`
- `tests/Host/MigrationComposition/Manifest/ManifestTests.cs`
- `src/Host/Experience/OfflineReconciliation/**`
- `tests/Host/Experience/OfflineReconciliation/**`
- `src/Clients/PosTerminal/src/routes/workspace.test.tsx`
- Yüzey devri (giriş): `database/MigrationComposition/order.json` ve
  `tests/Host/MigrationComposition/Manifest/ManifestTests.cs`, migration 050
  için `V1-IAM-024`'ten bu göreve devredildi (PO:2026-09-04).
- Paylaşılan dosyalarda sınırlı ek (V1-RMD-089/9. dalga deseni — sahiplik
  ilgili görevde kalır, bu görevde yalnızca sayılan değişiklik yapılır):
  `src/Modules/Identity/Authorization/Offline/PostgresOfflineAuthorityBudgetRepository.cs`
  (`V1-IAM-022` sahipliğinde kalır) — A1: yeniden-ihraçta önceki bütçeyi
  silme yerine `ORDER BY issued_at DESC LIMIT 1` okuma.
  `src/Modules/Identity/Authorization/Delegations/**`
  (`V1-IAM-021` sahipliğinde kalır) — D4: `RevokeAsync`'e
  `revokedByUserId` parametresi.
  `src/Host/Experience/Authorization/**` (`V1-IAM-020` sahipliğinde kalır) —
  D4: `AuthorizationDecisionStore.RevokeDelegationAsync` aktör parametresi
  alır, revoke endpoint'i `ActorId(http)`'yi geçirir; C3 ile aynı dizine
  eklenmez, yeni reconnect endpoint'i kendi dizininde yaşar.
  `src/Host/DualScreen/DualScreenApplication.Endpoints.cs` ve
  `src/Host/DualScreen/DualScreenApplication.cs`
  (`V1-IAM-024` sahipliğinde kalır) — C2: login handler'ına offline bütçe
  ihracı; composition kökünde `AddOfflineReconciliationExperience()` /
  `MapOfflineReconciliationApi()` iki satırı.
  `tests/Modules/Identity/Authorization/Grants/**` (`V1-IAM-021` sahipliğinde
  kalır) — D5: yeni bir dosya,
  `AuthorizationGrantsRateIndexMigrationTests.cs`, migration 052'nin metnini
  doğrular; mevcut dosyalar değişmez.
  `src/Host/Composition/Migrations/MigrationManifest.cs` içindeki
  `PhaseBMax` sabiti (`V1-FND-004` sahipliğinde kalır) — yalnızca faz üst
  sınırı 052 değerine güncellenir.
- Bu görev, başka bir task'in owned surface alanını başka şekilde
  değiştiremez.

## In scope

- **A1** Migration 050: `uq_offline_authority_budgets_session` düşürülür;
  `PostgresOfflineAuthorityBudgetRepository.CreateAsync` önceki bütçeyi
  silmeyi bırakır; `GetBySessionAsync` / `LoadAsync` `ORDER BY issued_at DESC
  LIMIT 1` alır.
- **D4** Migration 051: `identity.authorization_delegations`'a
  `revoked_by_user_id UUID` eklenir; `RevokeAsync(id, at, revokedByUserId)`;
  `AuthorizationDecisionStore.RevokeDelegationAsync(id, actorId)`; endpoint
  `ActorId(http)`'yi geçirir.
- **D5** Migration 052: `ix_authorization_grants_granted_rate` kısmi indeksi
  (`requester_user_id, permission_code, resolved_at) WHERE status = 'granted'`.
  Üç ayrı migration'a bölündü (050/051/052, tek bir "Aynı migration" değil)
  çünkü her biri farklı bir dar-zincir test fixture'ının (OfflineBudgetDatabase,
  DelegationDatabase) şemasına bağlanır.
- **C2** `DualScreenApplication.Endpoints.cs` login handler'ı, kullanıcının
  rolünü bulup `IOfflineAuthorityBudgetService.IssueAsync(userId, roleCode,
  sessionId)` çağırır ve bütçeyi (`budgetId`, `expiresAt`, `lines`) login
  yanıtına ekler; rolsüz kullanıcı (olmaması gereken durum) girişi
  başarısız etmez, yalnız `offlineBudget: null` alır.
- **C3** Yeni `POST /api/v1/terminals/{terminalId}/offline-reconciliation`
  endpoint'i (`src/Host/Experience/OfflineReconciliation/**`, bu göreve ait
  yeni bir Experience modülü) terminal-bağlı kasiyer oturumunu doğrular ve
  `IOfflineGrantReconciler.ReconcileAsync` çağırır; sonuç listesi
  (idempotency key, grant id, status, detail) döner.
- **D6** `workspace.test.tsx` (yeni dosya), `/authorization` rotasını bir
  `reports.view` yetenek kümesiyle render eder, üç listeyi (pending-grants,
  delegations, behavioural-tightenings) çeken fetch sarmalayıcısını ve bir
  onayla tıklamasının gerçek bir POST'a bağlandığını doğrular;
  `reports.view` yokken API'nin hiç çağrılmadığını doğrular.

## Out of scope

- D1-D3, D7, D8 (Phase 2 düşük riskli sağlamlaştırma) — ayrı `fix(v1-iam)`
  commit'i ile zaten kapatıldı.
- E1-E3 model doküman düzeltmeleri — `fix(v1-iam-016)` ile ayrıca kapatıldı.
- **C1, C4, C5** — `V1-IAM-026`'ya devredildi (yukarıdaki "Kapsam daraltıldı"
  notu): grant-class bir mutasyon endpoint'i (bill void/comp/discount) inşa
  etmek yeni iş özelliğidir, bu görevin "yalnız kablolama" sınırının dışında.
- P1 (personel provisioning bootstrap) — ayrı bir karar/görev.
- Yeni izin kodu; yalnız şema sağlamlaştırma + mevcut motorun kablolanması.

## Dependencies

- V1-IAM-018
- V1-IAM-019
- V1-IAM-020
- V1-IAM-021
- V1-IAM-022
- V1-IAM-023
- V1-IAM-024

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` ve `-c Debug`: 0 uyarı / 0 hata.
- `dotnet test` (yerel Postgres 18, `alkaros-test-pg` konteyneri):
  `ALKAROS.Identity.Authorization.Tests` 185/185; `ALKAROS.Host.Experience.Authorization.Tests`
  5/5; `ALKAROS.Host.Experience.OfflineReconciliation.Tests` 4/4 (yeni proje —
  oturumsuz istek 401; bilinmeyen `budgetId` 404 `UNKNOWN_BUDGET`; bütçe içi
  eylem `Pending` + `offline_pending_review` detayıyla döner);
  `ALKAROS.Host.Experience.Composition.Tests` 4/4; `ALKAROS.Host.Experience.Billing.Tests`
  3/3; `ALKAROS.Architecture.Tests` 8/8.
- Üç yeni migration (050/051/052) `docker exec alkaros-test-pg psql` ile
  001..047 zincirinden sonra ayrı ayrı ileri uygulandı ve şekil doğrulandı
  (`\d` çıktısı: kısıtlama düşmüş, yeni kolon+CHECK, yeni indeks); sonra 052,
  051, 050 sırayla geri alındı ve önceki şekle dönüldüğü doğrulandı.
  `PhaseBMax` 049 → 052.
- Frontend: `tsc --noEmit` temiz; `vitest run` 109/109 (PosTerminal; yeni
  `workspace.test.tsx` 3 test — `reports.view` ile üç liste çekilir ve boş
  hazır durumu render edilir; `reports.view` yokken API hiç çağrılmaz; bir
  onayla tıklaması gerçek bir POST'a bağlanır).
- `python tools/plan-audit/plan_audit_tool.py validate` / `verify-manifest` /
  `validate-coverage` sıfır hata; `python tools/consistency-audit/consistency_audit.py`
  temiz (yol boyunca bulunan 2 önceden var olan ihlal de ayrı bir
  `fix` commit'iyle kapatıldı).
- Ortam istisnaları: `ALKAROS.Host.Tests` `Execution.MigrationExecutionTests`
  yerelde `psql` PATH'te olmadığı için atlandı (G2, CI'da geçer); yerine
  yukarıdaki doğrudan `docker exec psql` doğrulaması yapıldı.
  `DualScreenHostTests`'in login akışı da aynı nedenle yerelde koşmadı; C2
  kod yolu (`IssueAsync`, rol çözümü) tamamen birim test kapsamındadır.
- C1/C4/C5 kapsam dışı bırakıldı; bkz. Goal ve Out of scope.

## Handoff

- V1-IAM-026
- V1-GOV-072

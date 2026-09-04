# V1-IAM-025 - Authorization wave hardening and requester-side wiring

- Task ID: V1-IAM-025
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

`V1-IAM-016..024` dalgasını denetleyen
`docs/engineering/authz-wave-remediation-plan.md` Phase 3 ve 5'in kapsamını
tamamlar (Phase 2, D1-D3/D7/D8, ayrı `fix(v1-iam)` commit'i olarak zaten
kapatıldı): (a) üç migration (050/051/052) ile şema sağlamlaştırması (A1
offline bütçe yeniden-ihraç FK çökmesi, D4 delegation revoke actor'ı, D5
davranışsal oran sorgusu indeksi); (b) inşa edilmiş ama hiçbir canlı istek yoluna
bağlanmamış yetkilendirme motorunun (grant request, offline bütçe ihracı,
reconciliation) istemci tarafını bağlamak (C1-C5) ve `workspace.tsx`
`/authorization` rota kablolaması testi (D6). Motor bileşenleri
`V1-IAM-018`..`V1-IAM-023` ile teslim edildi ve DI-kayıtlı + birim/entegrasyon
test edildi, ama hiçbir Experience endpoint'i
`IAuthorizationGrantService.RequestAsync` çağırmıyor, login hiçbir offline
bütçe ihraç etmiyor ve hiçbir reconnect endpoint'i reconciliation'ı
tetiklemiyor — bu görev o boşluğu kapatır.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-025-authorization-wave-hardening-and-requester-wiring.md`
- `database/migrations/V1/V1-IAM-025/**`
- `database/MigrationComposition/order.json`
- `tests/Host/MigrationComposition/Manifest/ManifestTests.cs`
- `src/Host/Experience/OfflineReconciliation/**`
- `tests/Host/Experience/OfflineReconciliation/**`
- `evidence/V1-IAM-025/**`
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
  `src/Host/Experience/Billing/BillingSplitApplication.cs`
  (`V1-IAM-024` sahipliğinde kalır) — C1: doğrudan izni olmayan çağıran için
  `IAuthorizationGrantService.RequestAsync` dalı.
  `src/Host/DualScreen/DualScreenApplication.Endpoints.cs`
  (`V1-IAM-024` sahipliğinde kalır) — C2: login handler'ına offline bütçe
  ihracı.
  `src/Host/Experience/Tables/TableManagementApplication.cs` +
  `TableManagementContracts.cs` (`V1-IAM-024` sahipliğinde kalır) — C4:
  `AllowedCommands` tutulan izin ∪ ulaşılabilir yetki isteği birleşimi.
  `src/Clients/PosTerminal/src/routes/workspace.tsx` ile ilgili test dosyası
  (`V1-RMD-097` sahipliğinde kalır) — D6: rota kablolaması testi.
  `tests/Modules/Identity/Authorization/Grants/**` (`V1-IAM-021` sahipliğinde
  kalır) — D5: yeni bir dosya,
  `AuthorizationGrantsRateIndexMigrationTests.cs`, migration 052'nin metnini
  doğrular; mevcut dosyalar değişmez.
  `src/Host/Composition/Migrations/MigrationManifest.cs` içindeki
  `PhaseBMax` sabiti (`V1-FND-004` sahipliğinde kalır) — yalnızca faz üst
  sınırı 050 değerine güncellenir.
- Bu görev, başka bir task'in owned surface alanını başka şekilde
  değiştiremez.

## In scope

- **A1** Migration 050: `uq_offline_authority_budgets_session` düşürülür;
  `PostgresOfflineAuthorityBudgetRepository.CreateAsync` önceki bütçeyi
  silmeyi bırakır; `GetBySessionAsync` / `LoadAsync` `ORDER BY issued_at DESC
  LIMIT 1` alır; "reconciliation sonrası yeniden ihraç" DB testi eklenir.
- **D4** Migration 051: `identity.authorization_delegations`'a
  `revoked_by_user_id UUID` eklenir; `RevokeAsync(id, at, revokedByUserId)`;
  `AuthorizationDecisionStore.RevokeDelegationAsync(id, actorId)`; endpoint
  `ActorId(http)`'yi geçirir; DB testi actor'ün saklandığını doğrular.
- **D5** Migration 052: `ix_authorization_grants_granted_rate` kısmi indeksi
  (`requester_user_id, permission_code, resolved_at) WHERE status = 'granted'`;
  migration-metni testi indeksi doğrular. Üç ayrı migration'a bölündü (050/051/052,
  tek bir "Aynı migration" değil) çünkü her biri farklı bir dar-zincir test
  fixture'ının (OfflineBudgetDatabase, DelegationDatabase) şemasına bağlanır.
- **C1** Billing `bills.void`/`bills.comp` endpoint'i, çağıran doğrudan izne
  sahip değilse `IAuthorizationGrantService.RequestAsync` çağırır ve 403
  yerine grant id + idempotency key ile `pending` yanıt döner; aynı
  idempotency key ile onay sonrası yeniden gönderim işlemi tamamlar.
- **C2** `DualScreenApplication.Endpoints.cs` login handler'ı
  `IOfflineAuthorityBudgetService.IssueAsync(userId, roleCode, sessionId)`
  çağırır ve bütçeyi istemciye döner.
- **C3** Yeni `POST /api/v1/terminals/{id}/offline-reconciliation` endpoint'i
  (`src/Host/Experience/OfflineReconciliation/**`, bu göreve ait yeni bir
  Experience modülü) `IOfflineGrantReconciler.ReconcileAsync` çağırır.
- **C4** `TableContractMapper.AllowedCommands` "tutulan izinler ∪ ulaşılabilir
  yetki istekleri" olarak hesaplanır (model §6); istemci "Void (onay
  gerekiyor)" gösterebilir.
- **C5** C1'in sonucu: `BehaviouralTighteningGate` ile
  `DelegationEscalationResolver`'ın gerçek bir `RequestAsync` HTTP yolunda
  tetiklendiğini kanıtlayan entegrasyon testleri
  (`tests/Host/Experience/Billing/**`, mevcut sahibinde).
- **D6** `workspace.tsx`'e `/authorization` rotasını bir `reports.view`
  yetenek kümesiyle render eden ve fetch sarmalayıcısının store
  callback'lerini bağladığını doğrulayan bir test eklenir.

## Out of scope

- D1-D3, D7, D8 (Phase 2 düşük riskli sağlamlaştırma) — ayrı `fix(v1-iam)`
  commit'i ile zaten kapatıldı.
- E1-E3 model doküman düzeltmeleri — `fix(v1-iam-016)` ile ayrıca kapatıldı.
- P1 (personel provisioning bootstrap) — ayrı bir karar/görev; bu görevin
  kapsamına yalnız implementasyon sırasında açıkça genişletilirse girer.
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

- `dotnet build ALKAROS.slnx -c Release` sıfır uyarı / sıfır hata; tam
  `dotnet test` paketi geçer (yerel Postgres 18).
- Üç yeni migration (050/051/052) ileri ile tam geri yönde boş veritabanında
  denenir; `PhaseBMax` 052'ye güncellenir.
- C1-C5 için en az bir uçtan uca entegrasyon testi: doğrudan izni olmayan bir
  çağıran 403 yerine `pending` grant yanıtı alır; onay sonrası aynı
  idempotency key ile eylem tamamlanır; davranışsal kapı ve devir çözücü bu
  yolda tetiklenir.
- `python tools/plan-audit/plan_audit_tool.py validate` / `verify-manifest` /
  `validate-coverage` sıfır hata.

## Handoff

- V1-GOV-072

# V1-IAM-025 - Authorization wave hardening and requester-side wiring

- Task ID: V1-IAM-025
- Status: Blocked
- Assignee: Unassigned
- Work type: implementation
- Surface state: Planned

## Goal

`V1-IAM-016..024` dalgasını denetleyen
`docs/engineering/authz-wave-remediation-plan.md` Phase 2, 3 ve 5'in kapsamını
tamamlar: (a) düşük riskli kod sağlamlaştırmaları (D1-D3, D7, D8); (b) yeni bir
migration ile şema sağlamlaştırması (A1 offline bütçe yeniden-ihraç FK çökmesi,
D4 delegation revoke actor'ı, D5 davranışsal oran sorgusu indeksi); (c) inşa
edilmiş ama hiçbir canlı istek yoluna bağlanmamış yetkilendirme motorunun
(grant request, offline bütçe ihracı, reconciliation) istemci tarafını
bağlamak (C1-C5) ve `workspace.tsx` `/authorization` rota kablolaması testi
(D6). Motor bileşenleri `V1-IAM-018`..`V1-IAM-023` ile teslim edildi ve
DI-kayıtlı + birim/entegrasyon test edildi, ama hiçbir Experience endpoint'i
`IAuthorizationGrantService.RequestAsync` çağırmıyor, login hiçbir offline
bütçe ihraç etmiyor ve hiçbir reconnect endpoint'i reconciliation'ı
tetiklemiyor — bu görev o boşluğu kapatır.

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-025-authorization-wave-hardening-and-requester-wiring.md`
- `database/migrations/V1/V1-IAM-025/**`
- `src/Modules/Identity/Authorization/025/**`
- `tests/Modules/Identity/Authorization/025/**`
- `evidence/V1-IAM-025/**`
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## In scope

- **D1** `GrantRequest.Validate()` `RequesterUserId == Guid.Empty` durumunu
  reddeder (`OfflineAuthorizedAction.Validate()` ile tutarlı hale gelir).
- **D2** `PostgresAuthorizationPolicyRepository.UpsertAsync`: `expectedRowVersion`
  non-null iken `UPDATE ... WHERE row_version = @expected RETURNING`; sıfır
  satır -> `AuthorizationPolicyConcurrencyException`; `expectedRowVersion` null
  iken yalnız `INSERT`.
- **D3** `AuthorizationDecisionEndpointFilter.MapError`'a `ArgumentException` ->
  400 dalı eklenir (Catalog'un filtresiyle tutarlı).
- **D7** `OfflineGrantReconciler.StoreAsync` PostgreSQL `23503`'ü de yakalar ve
  tipli bir `UnknownOfflineAuthorityBudgetException` yüzeyler (bütçe
  reconcile ortasında silinmişse).
- **D8** `OfflineAuthorizedAction.Validate()` `OfflineAuthorizedAt == default`
  durumunu reddeder.
- **A1** Yeni migration: `uq_offline_authority_budgets_session` düşürülür;
  `PostgresOfflineAuthorityBudgetRepository.CreateAsync` önceki bütçeyi
  silmeyi bırakır; `GetBySessionAsync` / `LoadAsync` `ORDER BY issued_at DESC
  LIMIT 1` alır; "reconciliation sonrası yeniden ihraç" DB testi eklenir.
- **D4** Aynı migration: `identity.authorization_delegations`'a
  `revoked_by_user_id UUID` eklenir; `RevokeAsync(id, at, revokedByUserId)`;
  `AuthorizationDecisionStore.RevokeDelegationAsync(id, actorId)`; endpoint
  `ActorId(http)`'yi geçirir; DB testi actor'ün saklandığını doğrular.
- **D5** Aynı migration: `ix_authorization_grants_granted_rate` kısmi indeksi
  (`requester_user_id, permission_code, resolved_at) WHERE status = 'granted'`;
  migration-metni testi indeksi doğrular.
- **C1** En az bir mutasyon endpoint'i (Billing `bills.void`/`bills.comp`),
  çağıran doğrudan izne sahip değilse `IAuthorizationGrantService.RequestAsync`
  çağırır ve 403 yerine grant id + idempotency key ile `pending` yanıt döner;
  aynı idempotency key ile onay sonrası yeniden gönderim işlemi tamamlar.
- **C2** `DualScreenApplication.cs` login handler'ı
  `IOfflineAuthorityBudgetService.IssueAsync(userId, roleCode, sessionId)`
  çağırır ve bütçeyi istemciye döner.
- **C3** Yeni `POST /api/v1/terminals/{id}/offline-reconciliation` endpoint'i
  `IOfflineGrantReconciler.ReconcileAsync` çağırır.
- **C4** `TableContractMapper.AllowedCommands` "tutulan izinler ∪ ulaşılabilir
  yetki istekleri" olarak hesaplanır (model §6); istemci "Void (onay
  gerekiyor)" gösterebilir.
- **C5** C1'in sonucu: `BehaviouralTighteningGate` ile
  `DelegationEscalationResolver`'ın gerçek bir `RequestAsync` HTTP yolunda
  tetiklendiğini kanıtlayan entegrasyon testleri.
- **D6** `workspace.tsx`'e `/authorization` rotasını bir `reports.view`
  yetenek kümesiyle render eden ve fetch sarmalayıcısının store
  callback'lerini bağladığını doğrulayan bir test eklenir.

## Out of scope

- E1-E3 model doküman düzeltmeleri — `fix(v1-iam-016)` ile ayrıca kapatıldı.
- P1 (personel provisioning bootstrap) — ayrı bir karar/görev; bu görevin
  kapsamına yalnız implementasyon sırasında açıkça genişletilirse girer.
- Yeni iş kuralı veya izin kodu; yalnız sağlamlaştırma + kablolama.

## Dependencies

- V1-IAM-018
- V1-IAM-019
- V1-IAM-020
- V1-IAM-021
- V1-IAM-022
- V1-IAM-023
- V1-IAM-024

## Blocker

- Bu görev yürütmeye alınmadan önce, `In scope` altında sayılan mevcut sahipli
  dosyalar için custody devri kayıtları ilgili görevlere eklenmelidir:
  `src/Modules/Identity/Authorization/Grants/GrantRequest.cs` için
  `V1-IAM-023` (production), `tests/Modules/Identity/Authorization/Grants/**`
  için `V1-IAM-021` (test); `src/Modules/Identity/Authorization/Policies/**`
  için `V1-IAM-018`; `src/Host/Experience/Authorization/**` (endpoint filtresi
  + `AuthorizationDecisionStore` + `workspace.tsx` rota kablolaması) için
  `V1-IAM-020`; `src/Modules/Identity/Authorization/Delegations/**` için
  `V1-IAM-021`; `src/Modules/Identity/Authorization/Offline/**` için
  `V1-IAM-022`; `src/Modules/Identity/Authorization/Behavioural/**` için
  `V1-IAM-023`; `src/Host/Experience/Billing/BillingSplitApplication.cs`,
  `src/Host/Experience/Tables/TableManagementApplication.cs` +
  `TableManagementContracts.cs`, `src/Host/DualScreen/DualScreenApplication.cs`
  + `DualScreenApplication.Endpoints.cs` için `V1-IAM-024`. Ancak bu custody
  devri kayıtları `V1-IAM-024` deseniyle eklenip `plan/AUDIT_MANIFEST.json`
  yeniden üretildiğinde ve `validate` ile `verify-manifest` temiz kaldığında
  görev yeniden `Planned` yapılabilir.

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` sıfır uyarı / sıfır hata; tam
  `dotnet test` paketi geçer (yerel Postgres 18).
- Yeni migration ileri ile tam geri yönde boş veritabanında denenir;
  `PhaseBMax` güncellenir.
- C1-C5 için en az bir uçtan uca entegrasyon testi: doğrudan izni olmayan bir
  çağıran 403 yerine `pending` grant yanıtı alır; onay sonrası aynı
  idempotency key ile eylem tamamlanır; davranışsal kapı ve devir çözücü bu
  yolda tetiklenir.
- `python tools/plan-audit/plan_audit_tool.py validate` / `verify-manifest` /
  `validate-coverage` sıfır hata.

## Handoff

- V1-GOV-072

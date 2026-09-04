# V1-IAM-021 - Time Boxed Delegation

- Task ID: V1-IAM-021
- Status: Done
- Assignee: claude-session-01XKRazppo9sW452rdbCZsgy
- Work type: implementation
- Surface state: Existing

## Goal

`authorization_delegations` tablosu (grantee_user_id, permission_code,
limit_amount, expires_at, revoked_at) ile zaman sınırlı yetki devri kurulur: bir
yönetici belirli bir saate kadar sınırlı ikram/iskonto yetkisini bir kişiye
devreder. Süre dolması sorgu anında `expires_at` süzgeci ile uygulanır (arka
planda geri alma işi yoktur); `revoked_at` erken iptal içindir. Devir aktifken
tırmanan bir istek, yönetici kararına düşmeden `policy_path=delegation` ile
`granted` yazılır; süre dolunca ya da devir iptal edilince aynı istek yeniden
yöneticiye döner. Grant motoru, `Escalate` sonucundan sonra kayıtlı
`IEscalationResolver` zincirini yürür; `DelegationEscalationResolver` bu
zincirin ilk halkasıdır (`V1-IAM-022` ve `V1-IAM-023` yeni halkalar ekler).

## Owned surface

- `plan/v1/identity-authorization/V1-IAM-021-time-boxed-delegation.md`
- `database/migrations/V1/V1-IAM-021/**`
- `src/Modules/Identity/Authorization/Grants/**`
- `tests/Modules/Identity/Authorization/Grants/**`
- `src/Modules/Identity/Authorization/Delegations/**`
- `tests/Modules/Identity/Authorization/Delegations/**`
- `evidence/V1-IAM-021/**`
- Yüzey devri (giriş): src/Modules/Identity/Authorization/Grants/** ile tests/Modules/Identity/Authorization/Grants/**, tırmanma çözücü kancası (IEscalationResolver) ve AuthorizationGrantService'in çözücü yürüyüşü için V1-IAM-019'dan bu göreve devredildi (PO:2026-09-04). database/MigrationComposition/order.json ve tests/Host/MigrationComposition/Manifest/ManifestTests.cs migration 046 için, src/Modules/Identity/IdentityModule.cs ise DI kayıt evi olarak V1-IAM-019'dan bu göreve devredildi ve iş kapandıktan sonra V1-IAM-022'ye geçti (PO:2026-09-04).
- Yüzey devri (çıkış): database/MigrationComposition/order.json, tests/Host/MigrationComposition/Manifest/ManifestTests.cs (migration 047) ve src/Modules/Identity/IdentityModule.cs, bu görev kapandıktan sonra V1-IAM-022'ye devredildi (PO:2026-09-04).
- src/Host/Composition/Migrations/MigrationManifest.cs içindeki PhaseBMax sabiti V1-FND-004 sahipliğinde kalır; bu görevde yalnızca faz üst sınırı 046 değerine güncellenmişti (V1-RMD-089/9. dalga deseni).
- Bu görev, başka bir task'in owned surface alanını değiştiremez.

## Dependencies

- V1-IAM-019

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release`: 0 uyarı / 0 hata.
- `dotnet test` (yerel Postgres 18): `ALKAROS.Identity.Authorization.Tests`
  124/124 (yeni `Delegations/**` 18 test — `AuthorizationDelegationModelTests`
  saf mantık (`IsActiveAt` / `Covers` para+izin+süre; `DelegationRequest.Validate`
  kendine devir, negatif limit, geçmiş süre reddi); `AuthorizationDelegationMigrationTests`
  metin (window/limit/revoke/self CHECK'leri, `ix_authorization_delegations_active`
  kısmi index, seed satırı yok); `PostgresAuthorizationDelegationRepositoryTests`
  ile `AuthorizationDelegationsDownMigrationTests` gerçek 005..046 zinciriyle:
  create + `FindCovering`, tablo CHECK'inin kendine devri reddi, `FindCovering`
  para/süre/iptal süzgeci, birden çok devirde en yenisini döndürme, `ListActive`
  iptal+dolmuşu dışlama, down migration `authorization_delegations`'ı düşürüp
  `authorization_grants`'ı bırakır; `DelegationEscalationResolverTests` gerçek
  repo + DB: çözücü kapsayan aktif devirde `Delegation` yolu / kapsam yoksa
  null, grant servisi tırmanan isteği yönetici yerine devirle `granted`
  yazar, devir tutarı aşılınca yine `pending`, iptal edilmiş devir artık
  yetkilendirmez); `Host.Tests` `Manifest.ManifestTests` 16/16 (`PhaseBMax`
  046, 45 pozisyon, son giriş tabloları `["authorization_delegations"]`).
- Migration ileri: 001..046 zinciri boş `alkaros_fm4` veritabanına uygulandı;
  `identity.authorization_delegations` oluştu. Geri: `046-*.down.sql`
  uygulandı; tablo ile kısmi index düştü, `identity.authorization_grants`
  yerinde kaldı.
- Semih için gerçek senaryo: yönetici bir garsona 2 saatliğine `bills.comp`
  için ≤ ₺200 devir açar. Garson kendi çekinde ₺120 ikram ister; politika
  tırmanmaya düşürür, `DelegationEscalationResolver` devri bulur ve istek
  yönetici kararına gitmeden `granted` / `policy_path=delegation` yazılır
  (`approver_user_id` boş). Aynı garson ₺150 isterken devir ₺100 ile
  sınırlıysa istek yeniden `pending` olur. Devir iptal edilirse (revoked_at)
  ya da 2 saat dolunca `FindCovering` boş döner ve akış yeniden yöneticiye
  düşer.

## Handoff

- V1-IAM-022

# V1-RMD-121 - Shared manager/supervisor session authentication

- Task ID: V1-RMD-121
- Status: Done
- Assignee: claude-session-01GGsiy81vQBnzkRKVfPsjMC
- Work type: implementation
- Surface state: Existing

## Goal

Semih onayıyla ("Bu maddeleri tamamla" — Dalga 3'ün [Boundary] kendi taze
denetiminde bulunup bilerek kapsam dışı bırakılan bir madde), `identity.device_sessions`'a
karşı manager/supervisor oturum kimlik doğrulaması yapan üç ayrı sınıfın
(`RoleManagementAuthentication`, `AuthorizationDecisionAuthentication`,
`CatalogManagerAuthentication`) birbirinin birebir aynısı SQL'i (yalnızca
supervisor dahil edilip edilmediği farklı) üç yerde bağımsız olarak taşımasını
tek bir paylaşılan yardımcıya çıkarır. Bir sınır ihlali değil (üçü de identity'nin
kendi şemasını kendi oturum kontrolü için okuyor/güncelliyor, V0-ARC-001'in
her zaman izin verdiği bir şey) — gerçek risk, üçünden birine yapılacak bir
düzeltmenin diğer ikisine sessizce yansımaması.

## Owned surface

- `plan/v1/remediation/V1-RMD-121-shared-manager-session-authentication.md` (yeni)
- `src/Host/Experience/ManagementSessionLookup.cs` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - src/Host/Experience/Roles/RoleManagementEndpoints.cs (V1-RMD-110
    sahipliğinde) — RoleManagementAuthentication artık ManagementSessionLookup'ı
    çağırıyor; kullanılmayan using kaldırıldı.
  - src/Host/Experience/Authorization/AuthorizationDecisionEndpoints.cs
    (ilgili görev sahipliğinde) — AuthorizationDecisionAuthentication aynı
    şekilde.
  - src/Host/Experience/Catalog/CatalogManagementEndpoints.cs (ilgili görev
    sahipliğinde) — CatalogManagerAuthentication aynı şekilde.

## In scope

- `ManagementSessionLookup.ResolveActorAsync(dataSource, rawToken, allowSupervisor, ct)`:
  tek bir SQL sorgusu (manager-only veya manager-veya-supervisor, `allowSupervisor`'a
  göre), `last_seen_at`'i güncelleyip aktör id'sini döndürüyor, aksi halde null.
- Üç `*Authentication` sınıfının `AuthenticateAsync`'i bu tek yardımcıyı çağıracak
  şekilde sadeleştirilmesi — her biri kendi özel `*UnauthorizedException` tipini
  fırlatmaya devam ediyor (davranış değişmedi, yalnız SQL tekrarı kalktı).

## Out of scope

- Üç sınıfın kendi ayrı registration/DI ömürlerini veya endpoint filtre
  mantığını değiştirmek.
- `identity.device_sessions` şemasının kendisi veya `DeviceSessionToken` hash
  mekanizması.

## Dependencies

- V1-RMD-120

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 uyarı / 0 hata.
- `docker compose -f compose.yaml -f compose.test.yaml up --build test`:
  gerçek container exit code `docker inspect` ile doğrulandı (0);
  `ALKAROS.Host.Experience.Roles.Tests`, `ALKAROS.Host.Experience.Authorization.Tests`,
  `ALKAROS.Host.Experience.Catalog.Tests` davranış değişmeden yeşil.
- `python tools/consistency-audit/consistency_audit.py`: 13 ihlal, hepsi bu
  görevden önce de vardı, dokunulmayan dosyalarda.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata.

## Handoff

- V1-GOV-119

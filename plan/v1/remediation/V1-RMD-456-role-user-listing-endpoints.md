# V1-RMD-456 - Rol, izin ve kullanıcı listeleme uçları; rol oluşturma yeni rolün kimliğini döndürür

- Task ID: V1-RMD-456
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-30

## Goal

V1-RMD-449 (Personel ve roller bölümü) çalışırken görüldü: `/api/v1/management/roles` ve `/api/v1/management/users`
yalnız yazma uçlarına sahip. Rol, izin ve kullanıcıları listeleyen hiçbir uç yok ve `POST /roles/roles` yeni rolün
kimliğini döndürmüyor (204). Bu yüzden bir yönetici, rol atama ve izin verme uçlarının gerektirdiği `roleId` ve
`userId` değerlerini hiçbir istemciden öğrenemiyor; rol yönetimi ekranı bu uçlar olmadan yapılamaz.

Bu görev yalnız sunucu tarafını ekler: rolleri (izin kodlarıyla), izin kataloğunu ve kullanıcıları (kullanıcı adı, görünen ad,
etkin mi, rol kimlikleri; parola özeti asla) listeleyen salt-okunur uçlar ve rol oluşturmanın yeni `roleId`'yi döndürmesi.

## Owned surface

- `plan/v1/remediation/V1-RMD-456-role-user-listing-endpoints.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Roles/RoleManagementEndpoints.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/IRoleRepository.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/PostgresRoleRepository.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/IRoleManagementService.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/RoleManagementService.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Roles/RoleManagementHttpTests.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/RoleManagementServiceTests.cs

## In scope

- `GET /api/v1/management/roles/roles`: her rol için kimlik, kod, ad ve izin kodları; `identity.roles.manage` ister.
- `GET /api/v1/management/roles/permissions`: izin kataloğu (kod, ad); `identity.roles.manage` ister.
- `GET /api/v1/management/users`: kullanıcı kimliği, kullanıcı adı, görünen ad, etkin mi ve rol kimlikleri; `identity.users.manage` ister.
  Yanıtta parola özeti veya başka gizli alan bulunmaz.
- `POST /api/v1/management/roles/roles` yeni rolün `roleId` değerini döndürür (200); bu uca bugün hiçbir istemci bağlı değil.
- Her yeni uç için izinli ve izinsiz oturumu ayrı ayrı doğrulayan gerçek HTTP testi; hata gerekçeleri Türkçe.

## Out of scope

- İstemci ekranı (V1-RMD-457); rol silme, rol yeniden adlandırma, kullanıcı silme veya parola sıfırlama.
- Mevcut yazma uçlarının davranışı.

## Dependencies

- V1-RMD-449

## Acceptance evidence

- `dotnet build` ve ilgili test projeleri (`tests/Host`, `tests/Modules/Identity`) exit code 0; testler her üç listeleme ucunun izinli
  oturumda doğru kaydı, izinsiz oturumda 403'ü döndürdüğünü ve kullanıcı yanıtında parola özeti bulunmadığını doğrular.
  Çıktılar `evidence/V1-RMD-456/` altındadır.
- Gerçek Host ve veritabanıyla üç uç çağrılır ve yanıtlar kayda geçer (`gercek-deneme.log`).

## Handoff

- None

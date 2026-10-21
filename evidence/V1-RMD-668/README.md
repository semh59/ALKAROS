# V1-RMD-668 — Rol ve kullanıcı yönetiminin denetim kaydı ve son yönetici koruması

Kapanan bulgular (V1-RMD-597): B002 (Yüksek), B020, B022 (kırpma yarısı).

| Bulgu | Düzeltme | Kanıt testi |
|---|---|---|
| B002 | Yedi yönetim komutu başarıdan sonra `audit.audit_events` satırı yazar (`identity.role-created`, `permission-added`, `role-permission-assigned/revoked`, `user-role-assigned/revoked`, `user-created`); aktör kimliği, rol/kullanıcı kimliği, kodlar; parola yazılmaz | `EveryManagementCommandLeavesOneAuditRowNamingTheActor`, `ADeniedManagementCommandLeavesNoAuditRow` |
| B020 | `identity.roles.manage` için son aktif sahip yetkisinden ya da rolünden çıkarılamaz (409 `LAST_ROLE_MANAGER`, Türkçe ileti); pasif kullanıcı sayılmaz; yetki hiç kimsede yoksa engel yok | `RoleManagementGuardTests` (4 test), `TheLastRoleManagerCannotBeStrippedOfTheirOwnPermission` |
| B022 | Yeni kullanıcı adı baştaki/sondaki boşluktan arındırılır | `NewUserNamesAreStoredWithoutSurroundingSpaces` |

## Koşu (test-*.log)
Identity Authorization 228/228, Host Roles 15/15, Host.Errors 180/180, ApiConventions 5/5, ModuleBoundaries 10/10; 0 başarısız.

## Mutasyon (mutations.log, mutate.mjs)
15 mutant, 15'i öldü, her turda dosya orijinale döndürüldü ve cmp ile doğrulandı (7 olay adı, aktör, iki koruma çağrısı, eşik, aktiflik filtresi, kırpma, kullanıcı bazlı yok sayma, hata eşlemesi).

## Bilinen sınırlar
- Denetim yazımı komutla aynı işlemde değil: komut uygulandıktan sonra denetim yazımı başarısız olursa istek hata döner ama komut uygulanmıştır.
- Son yöneticiyi pasifleştirmek (Güvenlik yönetimi), kullanıcı adında büyük/küçük harf eşitliği, sahip olunmayan yetkiyi verme sınırı bu görevde yok (V1-RMD-672 / ürün kararı).
- Yönetim ekranında yeni 409 iletisi sunucunun Türkçe iletisiyle görünür; ekran testi bu görevde yok (V1-RMD-669).

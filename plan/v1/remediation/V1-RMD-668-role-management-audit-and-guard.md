# V1-RMD-668 - Rol ve kullanıcı yönetiminin denetim kaydı ve son yönetici koruması

- Task ID: V1-RMD-668
- Status: InProgress
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

V1-RMD-597 bulguları B002, B020 ve B022 (boşluk kırpma yarısı) kapanır. Rol oluşturma, yetki ekleme, role yetki verme ve geri alma, kullanıcıya rol verme ve geri alma, kullanıcı oluşturma artık denetim kaydı (`audit.audit_events`) bırakır: kim, kime, hangi rolü ya da yetkiyi, ne zaman. Rol yönetimi yetkisine (`identity.roles.manage`) sahip son aktif kişinin yetkisi ya da rolü geri alınamaz; ekran bunu Türkçe bir iletiyle (409 `LAST_ROLE_MANAGER`) reddeder. Yeni personel hesabının kullanıcı adındaki baştaki ve sondaki boşluklar kırpılır, böylece girişte kırpılan ad ile kayıtlı ad birbirini tutar.

## Owned surface

- `plan/v1/remediation/V1-RMD-668-role-management-audit-and-guard.md`
- `evidence/V1-RMD-668/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/RoleManagementService.cs - yalnız son yönetici koruması, kullanıcı adı kırpma ve istisna sınıfı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/IRoleRepository.cs - yalnız aktif yetki sahibi sayma
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/PostgresRoleRepository.cs - yalnız aktif yetki sahibi sayma
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Roles/RoleManagementEndpoints.cs - yalnız denetim kaydı yazımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Errors/ApiErrorCatalog.cs - yalnız rol yönetimi son yönetici hatası
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/RoleManagementServiceTests.cs - yalnız yeni testler
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Roles/RoleManagementHttpTests.cs - yalnız yeni testler
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-667-pin-lock-chain.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Yedi yönetim komutunun denetim olayları (`identity.role-created`, `identity.permission-added`, `identity.role-permission-assigned`, `identity.role-permission-revoked`, `identity.user-role-assigned`, `identity.user-role-revoked`, `identity.user-created`); olay komut başarılı olduktan sonra, aynı istekte yazılır.
- `identity.roles.manage` için son sahip koruması (yetkinin rolden geri alınması ve kullanıcıdan rolün geri alınması).
- Kullanıcı adı kırpma.

## Out of scope

- Denetim kaydının komutla aynı işlemde yazılması (komut ve denetim ayrı depolardan geçer; denetim yazımı başarısız olursa istek hata döner ama komut uygulanmıştır, bilinen sınır). Kullanıcıyı pasifleştirerek son yöneticiyi kaldırma (Güvenlik yönetimi; V1-RMD-672). Kullanıcı adında büyük/küçük harf ve Türkçe I/İ eşitliği (veritabanı dizini gerektirir; V1-RMD-672). Sahip olmadığı yetkiyi verme kuralı (ürün kararı). Yetki verme ekranının onayı (V1-RMD-669).

## Dependencies

- V1-RMD-667

## Acceptance evidence

1. Önce kırmızı test, sonra yeşil; Yüksek bulgu (B002) için mutasyon kanıtı.
2. `evidence/V1-RMD-668/` altında çıktılar; Identity Authorization, Host Roles ve ApiErrorCatalog testleri yeşil.

## Handoff

- V1-RMD-598

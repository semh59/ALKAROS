# V1-RMD-677 - Yetki limitleri ekranı ve sunucu uçları

- Task ID: V1-RMD-677
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

V1-RMD-597 bulgusu B016'nın ekran kısmı kapanır. Yönetim bölümündeki "Personel ve roller" sekmesine "Yetki limitleri" paneli eklenir. Yönetici, garson ve kasiyer için indirim, ikram ve iptal işlemlerinde "kendi onaylayabileceği en çok tutar, saatlik en çok işlem sayısı ve pencere süresi"ni görür ve değiştirir; "Yönetici onaylasın" ile limiti kaldırıp her isteği yöneticiye bağlayabilir. Her değişiklik onay sorar ve denetim kaydına yazılır. Sunucu tarafında yalnız rol yönetimi iznine sahip biri kullanabilir; tutar 1000 TL, adet 100, pencere 1-24 saatle sınırlıdır; "her şeyi otomatik onayla" kipi ekrandan açılamaz.

## Owned surface

- `plan/v1/remediation/V1-RMD-677-authorization-limits-screen.md`
- `evidence/V1-RMD-677/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-staff-roles/PoliciesPanel.tsx - yalnız yeni dosya
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-staff-roles/PoliciesPanel.test.tsx - yalnız yeni dosya
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Roles/RoleManagementEndpoints.cs - yalnız politika uçları ve istek sözleşmeleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Errors/ApiErrorCatalog.cs - yalnız politika hataları
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Roles/RoleManagementHttpTests.cs - yalnız yeni testler
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-staff-roles/api.ts - yalnız politika çağrıları
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-staff-roles/models.ts - yalnız politika tipleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-staff-roles/StaffSection.tsx - yalnız panelin yerleştirilmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-staff-roles/StaffSection.test.tsx - yalnız sahte istemciye yeni yöntemler
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-staff-roles/RolesPanels.test.tsx - yalnız sahte istemciye yeni yöntemler
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/features/management-staff-roles/management-staff-roles.css - yalnız panel stili
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Clients/PosTerminal/src/strings.ts - yalnız panel metinleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/domain/authorization-model.md - yalnız ekranın varlığı
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-676-offline-reconciliation-regressions.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Uçlar: `GET /api/v1/management/roles/policies`, `PUT .../policies/{permissionCode}/{roleCode}`, `DELETE .../policies/{permissionCode}/{roleCode}`; denetim olayları `identity.policy-changed` ve `identity.policy-removed`.
- Panel: garson ve kasiyer × indirim, ikram, iptal satırları; düzenleme, onay, kaldırma.

## Out of scope

- Davranışsal sıkılaştırma eşikleri, çevrimdışı bütçe satırlarının ekrandan düzenlenmesi (bütçe satırları bu politikalardan türer), başka roller (yönetici ve şef garson zaten bu izinlere sahiptir). Tarayıcıda elle deneme (doğrulanamadı).

## Dependencies

- V1-RMD-676

## Acceptance evidence

1. Sunucu testleri gerçek PostgreSQL'de, panel testleri vitest; mutasyon kanıtı (yetki, sınırlar, onay, denetim).
2. `evidence/V1-RMD-677/` altında çıktılar; Roles Host testleri, ApiConventions ve vitest yeşil.

## Handoff

- V1-RMD-598

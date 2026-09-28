# V1-RMD-408 - Çok rollü kullanıcıda onay kurallarını en kısıtlayıcı rolle değerlendirmek

- Task ID: V1-RMD-408
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-399 denetimi N-1: bir kullanıcıya birden fazla rol atanabiliyor (`POST .../roles/{roleId}/users/{userId}`), ama
grant değerlendirmesinde kullanılan rol `GetRoleIdsForUserAsync` sonucunun ilk elemanıydı ve sorguda `ORDER BY` yoktu.
Hem garson hem kasiyer olan birinin ikram isteğinde "garson yalnız kendi hesabında" kuralının ve hangi rol
politikasının uygulanacağı veritabanı satır sırasına bağlıydı. Semih'in kararı (2026-09-28): "en kısıtlayıcı rol
olsun".

Bu görev "yöneten rolü" tek yerde tanımlar: kullanıcının rolleri içinde doğrudan en az izne sahip olan rol; eşitlikte
rol kodunun sıralı küçüğü (tekrarlanabilir). İptal/ikram/indirim grant istekleri, çevrimdışı mutabakat ve girişte
verilen çevrimdışı bütçe ile ekrandaki rol adı bu rolü kullanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-408-governing-role-most-restrictive.md`
- `evidence/V1-RMD-408/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/IRoleRepository.cs ve
  src/Modules/Identity/Authorization/PostgresRoleRepository.cs (V1-RMD-110 sahipliğinde) — yalnız
  `GetGoverningRoleForUserAsync`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Orders/OrderManagementEndpoints.cs,
  src/Host/Experience/Billing/BillingSplitApplication.cs ve src/Host/DualScreen/DualScreenApplication.Endpoints.cs
  (V1-IAM-024 sahipliğinde) — yalnız rol seçimi satırları
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/OfflineReconciliation/OfflineReconciliationEndpoints.cs
  (V1-IAM-025 sahipliğinde) — yalnız rol seçimi satırları
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authorization/GoverningRoleTests.cs (yeni dosya,
  V1-IAM-002 test dizininde)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/Comp/OrderManagementCompHttpTests.cs ve
  tests/Host/Experience/Orders/Comp/OrderManagementCompTestDatabase.cs (V1-BIL-005 sahipliğinde) — çok rollü garson testi
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/domain/authorization-model.md (V1-IAM-016 sahipliğinde) — yalnız §3
  karar notu

## In scope

- Yöneten rol: en az doğrudan izin; eşitlikte rol kodu sıralı küçük; rolsüz kullanıcıda yok.
- Altı çağıran yer (`/comp`, `/void-sent`, indirim, çevrimdışı mutabakat, giriş, oturum bilgisi) bu yöntemi kullanır.

## Out of scope

- Doğrudan izin kontrolü (`AuthorizationService`): kullanıcının bütün rollerindeki izinlerin birleşimi değişmez.

## Dependencies

- V1-RMD-407

## Acceptance evidence

- Görev kapanışında bu bölüm gerçek koşu çıktılarıyla doldurulur.

## Handoff

- None

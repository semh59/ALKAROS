# V1-RMD-426 - Katalog, rol yönetimi ve karar uçlarındaki İngilizce hata mesajlarının Türkçeleştirilmesi

- Task ID: V1-RMD-426
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-399 yetki denetimi S-01 (Düşük, bilinen): katalog yönetimi ucunun hata eşlemesi İngilizce metin döndürüyor ve
PosTerminal bunu ekrana basıyordu (V1-RMD-131'de not edilmiş, açık görevi yoktu). docs/UI_STYLE_GUIDE.md §4 İngilizce
sızıntı taraması aynı kalıbı iki uçta daha buldu: rol yönetimi (ayrıca `InvalidOperationException` için ham
`exception.Message`, ör. "Role 'x' already exists.") ve onay/sıkılaştırma karar yüzeyi.

Bu görev: üç uçtaki bütün İngilizce hata mesajları Türkçeleştirilir; rol yönetimindeki ham istisna metni yerine sabit
bir Türkçe mesaj döner. Hata kodları değişmez.

## Owned surface

- `plan/v1/remediation/V1-RMD-426-english-error-messages-turkish.md`
- `evidence/V1-RMD-426/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Catalog/CatalogManagementEndpoints.cs,
  src/Host/Experience/Roles/RoleManagementEndpoints.cs ve
  src/Host/Experience/Authorization/AuthorizationDecisionEndpoints.cs (ilgili görevlerin sahipliğinde) — yalnız hata
  eşlemesindeki mesaj metinleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Catalog/CatalogManagementHttpTests.cs,
  tests/Host/Experience/Roles/RoleManagementHttpTests.cs ve
  tests/Host/Experience/Authorization/AuthorizationDecisionHttpTests.cs (ilgili görevlerin sahipliğinde) — mesaj
  doğrulamaları

## In scope

- Üç uçtaki hata mesajları; rol yönetiminde ham istisna metninin kaldırılması.

## Out of scope

- Hata kodları ve durum kodları.
- Diğer uçlar (tarama başka İngilizce hata metni bulmadı).

## Dependencies

- V1-RMD-425

## Acceptance evidence

- Kapanışta doldurulacak.

## Handoff

- None

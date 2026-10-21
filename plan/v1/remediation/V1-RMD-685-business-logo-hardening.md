# V1-RMD-685 - Logo: okuma yükü, denetim izi, imza ve boyut testleri

- Task ID: V1-RMD-685
- Status: InProgress
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

V1-RMD-598 bulguları R7, R8, R16 (logo kısmı) ve test kalitesi bulguları TK-B1, TK-B2, TK-B3, TK-B4 kapanır. Herkese açık /branding ve /logo logonun tamamını okumadan karar verir; logo yükleme ve silme aktörle denetim kaydına yazılır; imza ve boyut sınırı tam sınır değerleriyle test edilir; içerik türü büyük/küçük harften bağımsızdır; 136 göçünün sertleştirilmesi artımlı göç olarak ayrı görevdedir (V1-RMD-687).

## Owned surface

- `plan/v1/remediation/V1-RMD-685-business-logo-hardening.md`
- `evidence/V1-RMD-685/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Settings/BusinessIdentity/BusinessLogoStore.cs - yalnız meta okuma ve aktör kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Settings/BusinessIdentityLogoEndpoints.cs - yalnız aktör, tür karşılaştırma ve tek kopya okuma
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/QrOrdering/QrOrderingEndpoints.cs - yalnız /branding ve /logo okuma
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Settings/BusinessIdentityLogoEndpointsTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Settings/BusinessIdentity/BusinessLogoStoreTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Settings/SettingsManagementHttpTests.cs - yalnız ChangedBy ve DELETE hata testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-684-settings-edit-screen-and-names.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Meta okuma sorgusu, denetim izi (mevcut IAuditEventStore portu üzerinden), imza ve boyut testleri, ChangedBy ve moduleOwner süzgeci testleri.

## Out of scope

- Mevcut göçün değiştirilmesi (artımlı göç ayrı görev); çerez SameSite ve CSRF incelemesi (ayrı denetim).

## Dependencies

- V1-RMD-684

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-685/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-686

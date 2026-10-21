# V1-RMD-686 - Ayar geçmişi sağlamlaştırma, okuma önbelleği, test boşlukları, doküman sapmaları

- Task ID: V1-RMD-686
- Status: Done
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

V1-RMD-598 bulguları R9, R10, R11, R14, R17 ve kalan test kalitesi bulguları kapanır. Geçmiş sayfalanır ve ikincil sıralaması vardır (geçmişin veritabanında değişmezliği V1-RMD-687 göç görevindedir); garson özellik anahtarları tek sorguda okunur; ayar hizmetinin tekrarlı Host kayıtları silinir; GarsonFeatureToggles ve QrOrderExpirySetting için testler yazılır; settings-ownership.md koda göre düzeltilir.

## Owned surface

- `plan/v1/remediation/V1-RMD-686-settings-history-cache-tests-docs.md`
- `evidence/V1-RMD-686/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Settings/TypedSettings/PostgresSettingsRepository.cs - yalnız geçmiş sayfalama ve sıralama
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Settings/GarsonFeatureToggles/GarsonFeatureToggles.cs - yalnız toplu okuma
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Settings/SettingsModule.cs - yalnız kayıt
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Settings/SettingsManagementEndpoints.cs - yalnız geçmiş sayfalama
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/settings-ownership.md - yalnız koda uyum
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Settings/TypedSettings/PostgresSettingsRepositoryTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Settings/GarsonFeatureToggles/GarsonFeatureTogglesTests.cs - yalnız yeni dosya
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Settings/GarsonFeatureToggles/ALKAROS.Settings.GarsonFeatureToggles.Tests.csproj - yalnız yeni dosya
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Settings/QrOrderExpiry/QrOrderExpirySettingTests.cs - yalnız yeni dosya
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Settings/QrOrderExpiry/ALKAROS.Settings.QrOrderExpiry.Tests.csproj - yalnız yeni dosya
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-685-business-logo-hardening.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Geçmiş sayfalama ve sıralama, toplu okuma, DI tekrarları, yeni testler, belge düzeltmesi.

## Out of scope

- Ayar önbelleği (ölçüm yok, doğrulayıcı Düşük saydı; ölçüm sonrası ayrı karar).

## Dependencies

- V1-RMD-685

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-686/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-687

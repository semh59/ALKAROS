# V1-RMD-683 - Ayar değer doğrulaması: aralık, uzunluk, pasif ayar, kayıt yarışı

- Task ID: V1-RMD-683
- Status: InProgress
- Assignee: claude-code-session_01MwPj6Y6uC4Kbceoi6Rzzby
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-21

## Goal

V1-RMD-598 bulguları R4, R5, R6, R13, R15 kapanır. Her tipli ayarın alt ve üst sınırı vardır (masa sınırı 0-50, mutfak eşiği 1-99, QR süresi 1-1440 dk, ad uzunluğu, palet anahtarı), Duration yalnız açık biçim kabul eder, Reason ve değer uzunluğu sınırlıdır; pasif ayara PUT 409 döner ve yeniden etkinleştirme ucu vardır; ayar kaydı yarışta çökmez (ON CONFLICT); sır yasağı yazım varyantlarını yakalar; settings.manage izni katalogda sabittir.

## Owned surface

- `plan/v1/remediation/V1-RMD-683-settings-value-validation.md`
- `evidence/V1-RMD-683/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Settings/TypedSettings/SettingValidator.cs - yalnız doğrulama
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Settings/TypedSettings/PostgresSettingsRepository.cs - yalnız pasif ayar, kayıt yarışı ve etkinleştirme
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Settings/TypedSettings/SettingsService.cs - yalnız bozuk değerde günlük
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Settings/TypedSettings/SettingRequests.cs - yalnız uzunluk sınırları
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Settings/SettingsManagementEndpoints.cs - yalnız doğrulama, 409 ve etkinleştirme ucu
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Errors/ApiErrorCatalog.cs - yalnız ayar yönetimi eşlemesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authorization/Catalog/ApplicationPermissions.cs - yalnız settings.manage sabiti
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/CustomerCredit/CustomerCreditTermsEndpoints.cs - yalnız izin sabiti kullanımı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Settings/TypedSettings/PostgresSettingsRepositoryTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Settings/SettingsManagementHttpTests.cs - yalnız yeni test
- Sınırlı ek (paylaşılan, geri-tik olmadan): plan/v1/remediation/V1-RMD-682-table-limit-counts-open-tables.md - yalnız Handoff satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Aralık kataloğu ve doğrulama, pasif ayar 409 ve etkinleştirme, ON CONFLICT, sır listesi, izin sabiti, her biri için test.

## Out of scope

- Ayar düzenleme ekranı (V1-RMD-684).

## Dependencies

- V1-RMD-682

## Acceptance evidence

1. Her düzeltme önce kırmızıya dönen testle, sonra yeşil koşuyla; mutasyon kanıtı.
2. `evidence/V1-RMD-683/` altında çıktılar; ilgili test projeleri yeşil.

## Handoff

- V1-RMD-684

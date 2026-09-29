# V1-RMD-405 - Personeli pasifleştirme ve yeniden etkinleştirme

- Task ID: V1-RMD-405
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-399 denetimi N-2 (Orta) ve T-01: personeli pasifleştiren hiçbir ürün yolu yok; kaynakta
`identity.users.active = false` yazan kod bulunmuyor. İşten ayrılan personel için yalnız "tüm oturumları kapat"
(`revoke-sessions`) var; kişi parolasıyla hemen yeniden giriş yapabiliyor. Ayrıca pasif kullanıcının mevcut kasiyer
oturumunu reddeden kontrolü sınayan test yok (denetimin kör kalibrasyonu bu koşulu silen hatayı yalnız denetim
probe'uyla yakalayabildi).

Bu görev güvenlik yönetimi alanına (`security.manage`, yönetici) iki uç ekler: kullanıcıyı pasifleştirme (hesap
kapatılır, bütün cihaz oturumları hemen iptal edilir, giriş reddedilir) ve yeniden etkinleştirme. Kişi kendi hesabını
pasifleştiremez. Her iki işlem de mevcut güvenlik denetim akışına (`security.*` denetim olayı) yazılır.

## Owned surface

- `plan/v1/remediation/V1-RMD-405-staff-deactivation.md`
- `evidence/V1-RMD-405/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authentication/IUserStore.cs (V15-SEC-002
  sahipliğinde) — yalnız `SetActiveAsync`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Identity/Authentication/PostgresUserStore.cs (V1-IAM-004
  sahipliğinde) — yalnız `SetActiveAsync`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Security/IdentityHardening/AccountRecoveryService.cs ve
  src/Modules/Security/IdentityHardening/SuspiciousLoginEvent.cs (V15-SEC-002 sahipliğinde) — yalnız pasifleştirme /
  yeniden etkinleştirme ve iki yeni denetim nedeni
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/SecurityAdministrationEndpoints.cs
  ve src/Host/Experience/SecurityAdministration/AuditEventStoreSuspiciousLoginSink.cs (V1-RMD-266 sahipliğinde) — yalnız
  iki yeni uç ve iki yeni denetim olayı adı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Identity/Authentication/AuthenticationTimingContractTests.cs
  (V15-SEC-002 sahipliğinde) — yalnız sahte deponun yeni arayüz üyesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Security/IdentityHardening/AccountRecoveryServiceTests.cs
  (V15-SEC-002 sahipliğinde) — pasifleştirme testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/SecurityAdministration/SecurityAdministrationHttpTests.cs
  (V1-RMD-266 sahipliğinde) — uç testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs
  (V1-RMD-175 sahipliğinde) — pasif kullanıcının mevcut kasiyer oturumunun reddi (T-01)

## In scope

- `POST /api/v1/management/security/users/{userId}/deactivate`: `active = false`, bütün cihaz oturumları iptal,
  `security.account-deactivated` denetim olayı; kendi hesabı 409; bilinmeyen kullanıcı 404.
- `POST /api/v1/management/security/users/{userId}/reactivate`: `active = true`, `security.account-reactivated`
  denetim olayı; bilinmeyen kullanıcı 404.
- Mevcut `users/lookup` yanıtı zaten `active` alanını döndürür.

## Out of scope

- PosTerminal yönetim ekranı (V1-RMD-296 yönetim alanı kararı kapsamında).
- Pasif kullanıcının verisinin KVKK süresi sonunda silinmesi (mevcut saklama işi değişmez).

## Dependencies

- V1-RMD-404

## Acceptance evidence

- Dört test projesi Release, 0 uyarı / 0 hata; gerçek PostgreSQL 18 (`evidence/V1-RMD-405/tests.log`):
  `ALKAROS.Identity.Authentication.Tests` 59/59, `ALKAROS.Security.IdentityHardening.Tests` 15/15,
  `ALKAROS.Host.Experience.SecurityAdministration.Tests` 28/29 — düşen tek test yedekten geri yükleme tatbikatıdır ve
  değişikliksiz tabanda da aynı nedenle düşer (kapsayıcıda `pg_dump` 16, sunucu 18; aynı dosyada not edildi).
  `ALKAROS.Host.Tests` seçili giriş testleri 3/3.
- Yeni testler: servis — pasifleştirme hesabı kapatır, bütün oturumları iptal eder, denetler; yeniden etkinleştirme;
  bilinmeyen kullanıcı `false` ve denetim yok. HTTP — `security.manage` tutmayan yönetici 403; yönetici pasifleştirir
  (oturum sayısı 0, `active = false`, `security.account-deactivated`), yeniden etkinleştirir
  (`security.account-reactivated`), bilinmeyen kullanıcı 404; kendi hesabını pasifleştirme 409 `SELF_DEACTIVATION`.
- T-01: `ADeactivatedUsersExistingCashierSessionIsRefusedOnTheNextRequest` (iptal edilmemiş oturum, hesap pasif → 401);
  V1-RMD-399 kalibrasyon tohumu uygulanınca bu test kırmızıya döner (`evidence/V1-RMD-405/calibration-seed-caught.log`).
- Semih'in elle deneyebileceği senaryo: ayrılan bir personeli kullanıcı aramadan bulup pasifleştirmek; o kişinin açık
  tableti bir sonraki işlemde oturumdan düşer ve kişi parolasıyla giriş yapamaz; yeniden etkinleştirince tekrar girer.

## Handoff

- None

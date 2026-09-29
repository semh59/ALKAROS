# V1-RMD-406 - 8 saatlik oturum kararını giriş ve PIN ile kilit açmada uygulamak

- Task ID: V1-RMD-406
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-28

## Goal

V1-RMD-399 denetimi H-01: Semih'in kararı (V1-IAM-031, 2026-09-15) personel oturumunun 8 saat sürmesiydi; görev
yalnız `SessionTokenIssuer.DefaultLifetime`'ı 8 saate indirdi. Ancak gerçek giriş ucu (`/api/v1/auth/login`) kasiyer
ve yönetim cihaz oturumlarını, PIN ile kilit açma (`/api/v1/auth/unlock`) da döndürülen oturumu kendi içinde yazılı
`TimeSpan.FromHours(12)` ile açıyor; denetim probe'u gerçek girişten sonra 12,00 saat ölçtü. Bu görev bu üç yeri
`SessionTokenIssuer.DefaultLifetime`'a bağlar; karar tek bir sabitte kalır.

## Owned surface

- `plan/v1/remediation/V1-RMD-406-session-lifetime-at-login.md`
- `evidence/V1-RMD-406/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.Endpoints.cs
  (V1-IAM-024 sahipliğinde) — yalnız üç oturum ömrü ifadesi ve ilgili yorum
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs
  (V1-RMD-175 sahipliğinde) — giriş oturum ömrü testi

## In scope

- Kasiyer oturumu, yönetim (yönetici / şef garson) oturumu ve PIN ile kilit açma rotasyonu: 8 saat.

## Out of scope

- Müşteri ekranı oturumu (personel oturumu değil; `DualScreenStore.DisplaySessionLifetime`).
- QR müşteri oturumu ve masa jetonu ömürleri.

## Dependencies

- V1-RMD-405

## Acceptance evidence

- `ALKAROS.Host.Tests` DualScreen sınıfları (gerçek PostgreSQL 18, Release, 0 uyarı / 0 hata): 78/78
  (`evidence/V1-RMD-406/tests.log`).
- Yeni test `LoginIssuesStaffSessionsThatLiveEightHours`: gerçek girişte kasiyer ve yönetim çerezlerinin ikisi de
  7,9–8,05 saat içinde sona erer. Üretim değişikliği geri alınınca kırmızı: ölçülen 12,0004 saat (aynı dosya).
- Semih'in elle deneyebileceği senaryo: bir garson oturum açar; 8 saat sonra bir sonraki işlemde oturum düşer ve
  giriş ekranı gelir (önceden 12 saat sürüyordu). PIN ile kilit açmak oturumu yeniden 8 saatlik ömürle yeniler.

## Handoff

- None

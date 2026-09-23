# V15-SEC-002 - Implement identity abuse protections

- Task ID: V15-SEC-002
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.38-I.44
- PDF:II.11-II.12
- PDF:III.33-III.34
- EXT:OWASP-ASVS-5.0.0
- EXT:OWASP-AUTH
- EXT:OWASP-SESSION

## Goal

Oturum açma kısıtlaması, kilitleme politikası, oturum rotasyonu ve idari iptal ekleyin.

## Owned surface

- `src/Modules/Security/IdentityHardening/**`, `tests/Modules/Security/IdentityHardening/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  src/Modules/Identity/DeviceSessions/IDeviceSessionRepository.cs,
  PostgresDeviceSessionRepository.cs, IDeviceSessionService.cs, DeviceSessionService.cs
  (V1-IAM-003 sahipliğinde) — yeni `RevokeAllForUserAsync`/`RevokeAllAsync` metodları eklendi
  (mevcut `RevokeForDeviceAsync`/`RevokeDeviceAsync` deseninin aynısı, tek-cihaz yerine
  kullanıcının tüm cihazları); mevcut metodlar değişmedi, yeni migration yok.
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  src/Modules/Identity/Authentication/IUserStore.cs,
  PostgresUserStore.cs (V1-IAM-001 sahipliğinde) — yeni `ForceUnlockAsync` metodu eklendi
  (mevcut `failed_login_attempts`/`locked_until` kolonlarını kullanır, yeni migration yok);
  mevcut metodlar değişmedi.
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  tests/Modules/Identity/Authentication/AuthenticationTimingContractTests.cs
  (V1-IAM-005 sahipliğinde) — yalnız `IUserStore`'un yeni `ForceUnlockAsync` üyesini uygulayan
  bir satır eklendi; mevcut testler değişmedi.
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  src/Modules/Security/ALKAROS.Security.csproj,
  src/Modules/Security/packages.lock.json (V15-SEC-001 sahipliğinde) — yeni
  `ALKAROS.Identity.csproj` proje referansı eklendi (IdentityHardening, Identity'nin
  `AuthenticationService`/`IUserStore`/`IDeviceSessionService`'i ile compose ediyor);
  bu referans değişikliği tests/Modules/Security/SecretRotation/packages.lock.json'ı da
  (V15-SEC-001 sahipliğinde) transitive olarak etkiledi, yalnız lock dosyası yeniden
  üretildi, kaynak kod değişmedi.
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  ALKAROS.slnx (V1-FND-001 sahipliğinde),
  build/project-manifest.json (V1-FND-007 sahipliğinde) — yeni
  `ALKAROS.Security.IdentityHardening.Tests` proje kaydı eklendi (C86/C88/C91 emsali).
- Bu görev, yukarıdakiler dışında başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Kaba kuvvet kontrolleri, jeton rotasyonu, tümünü iptal etme, şüpheli giriş denetimi ve güvenli kurtarma.

## Out of scope

- ayrı olarak onaylanmadıkça MFA ve provider gizli depolama.

## Dependencies

- V1-IAM-001
- V1-IAM-003
- V15-SEC-001

## Deliverables

- `src/Modules/Security/IdentityHardening/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Release` → 0 uyarı, 0 hata.
- `dotnet test tests/Modules/Security/IdentityHardening/ALKAROS.Security.IdentityHardening.Tests.csproj` (gerçek
  Postgres 18'e karşı) → 12/12 geçti: `SessionRotationServiceTests` (3 — yeni token verir + eskisi hemen başarısız
  olur, zaten iptal edilmiş token reddedilir, bilinmeyen token reddedilir), `AccountRecoveryServiceTests` (4 —
  tüm cihazlar iptal edilir ve hemen başarısız olur + ilgisiz kullanıcı etkilenmez, aktif oturum yokken de denetim
  kaydı düşer, kilit süre dolmadan force-unlock kaldırılır, bilinmeyen kullanıcıda false döner ve denetim kaydı
  düşmez), `SuspiciousLoginAuditingAuthenticationServiceTests` (5 — önceki hatası olmayan başarı denetlenmez,
  ≥3 önceki hatalı denemeden sonraki başarı denetlenir, kilitlenme denetlenir, eşik altı hata denetlenmez,
  **kilitleme mekaniği değişmez ve ilgisiz kullanıcı etkilenmez** — decorator'ın kendi lockout kararını hiç
  değiştirmediğini doğrudan kanıtlıyor).
- Regresyon: `ALKAROS.Identity.Authentication.Tests` 59/59, `ALKAROS.Identity.DeviceSessions.Tests` 20/20,
  `ALKAROS.Security.SecretRotation.Tests` 28/28 — hepsi yeni eklemelerden sonra hâlâ yeşil.
- `python tools/project-manifest/project_manifest_tool.py` → VALID (0 fark).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- Detaylı komut çıktıları: `evidence/V15-SEC-002/**`.
- **2026-09-23 not** (bağımsız denetim): `SecurityModule.cs`'e kayıtla artık
  `AccountRecoveryService`/`SessionRotationService`/
  `SuspiciousLoginAuditingAuthenticationService` gerçekten DI'dan
  çözülebiliyor (önceden modülün tamamı DI'a hiç bağlı değildi — ayrı bir
  mimari düzeltmeyle giderildi, bkz. `71f28599`). `SuspiciousLoginAuditing
  AuthenticationService` artık `/api/v1/auth/login`'in gerçek yolunda
  (`DualScreenApplication.Endpoints.cs`). Ama `AccountRecoveryService`'in
  `RevokeAllSessionsAsync`/`ForceUnlockAsync`'i hâlâ HİÇBİR HTTP endpoint'e
  bağlı değil — yönetici bu eylemleri bugün API/SQL bilmeden tetikleyemez;
  bu, ayrı bir Host-wiring görevi gerektiriyor (bu task'ın Owned surface'ı
  hiçbir zaman Host/HTTP dosyalarını kapsamadı).

## Handoff

- V20-SEC-001

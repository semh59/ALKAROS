# V1-IAM-001 - Implement user authentication

- Task ID: V1-IAM-001
- Status: Done
- Assignee: opencode-v1-iam-001
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.7-I.10
- PDF:II.2.1
- PDF:III.3

## Goal

Password verification, active-user check, login/logout ve secure session issuance davranışını uygulamak.

## Owned surface

- `src/Modules/Identity/Authentication/LoginResult.cs`
- `src/Modules/Identity/Authentication/PasswordHasher.cs`
- `src/Modules/Identity/Authentication/SessionTokenIssuer.cs`
- `src/Modules/Identity/Authentication/StoredUser.cs`
- `tests/Modules/Identity/Authentication/ALKAROS.Identity.Authentication.Tests.csproj`
- `tests/Modules/Identity/Authentication/PasswordHasherTests.cs`
- `tests/Modules/Identity/Authentication/SessionTokenIssuerTests.cs`
- `database/migrations/V1/V1-IAM-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Kullanıcı kimlik bilgileri depolama, parola karma politikası, oturum açma/oturumu kapatma ve tekrarlanan başarısız
  denemelerde oturum kilidi (lockout) uygulayan arıza yanıtları.

## Out of scope

- Rol izinleri, cihaz kaydı ve şifre sıfırlama workflow.

## Dependencies

- V1-FND-001
- V0-ARC-002

## Deliverables

- `src/Modules/Identity/Authentication/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Geçerli oturum açma başarılı oldu; geçersiz/etkin olmayan kullanıcı, kimlik bilgisi sızıntısı olmadan başarısız olur;
  saklanan değerler tuzlanmış şifre karmalarıdır.

## Handoff

- V1-IAM-002
- V1-IAM-003

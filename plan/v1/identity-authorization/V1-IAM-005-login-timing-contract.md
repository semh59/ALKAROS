# V1-IAM-005 - Enforce login timing equality contract

- Task ID: V1-IAM-005
- Status: Planned
- Assignee: opencode-v1-iam-005
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:II.2.1
- PDF:III.3
- CORR:C42

## Goal

Unknown/inactive kullanıcı ile bilinen kullanıcının yanlış-parola login
yolunun eşit iş (tam olarak bir PBKDF2 doğrulaması + sınırlı DB yazımı)
üretmesini yazılı güvenlik sözleşmesine bağlamak ve stopwatch tabanlı
kararsız zamanlama testini deterministik kanıtla değiştirmek.

## Owned surface

- `src/Modules/Identity/Authentication/AuthenticationService.cs` (V1-IAM-004'ten devredilmiştir, C42)
- `src/Modules/Identity/Authentication/PasswordHasher.cs` (V1-IAM-001'den devredilmiştir, C42)
- `tests/Modules/Identity/Authentication/AuthenticationServiceTests.cs` (V1-IAM-004'ten devredilmiştir, C42)
- `tests/Modules/Identity/Authentication/AuthenticationTimingContractTests.cs`
- `docs/engineering/login-timing-contract.md`
- `evidence/V1-IAM-005/**`

## In scope

- Güvenlik sözleşmesi: unknown/inactive login tam olarak bir PBKDF2
  doğrulaması yapar (dummy work factor = sistem default iteration sayısı);
  bilinen yanlış parola yolu aynı PBKDF2 işini + tek atomik failure-counter
  yazımını yapar; lockout yolunda ek PBKDF2 işi üretilmez. Sözleşme
  `docs/engineering/login-timing-contract.md` içinde yazılır.
- Work factor yakınsama politikası: kabul edilen kullanıcı hash'lerinin
  iteration sayısı sistem default'una yakınsar (rehash-on-login dahil).
  IUserStore/PostgresUserStore sözleşmesinde değişiklik gerekiyorsa ayrı
  plan değişikliğiyle yüzey devri yapılır; bu görev kapsam dışı kalır.
- Stopwatch tabanlı `UnknownUsernameLoginTakesComparableTimeToKnownUserLogin`
  ve `InactiveUserLoginTakesComparableTimeToKnownUserLogin` testleri kaldırılır;
  yerine inject edilebilir store/verifier ile her yolun tam olarak bir PBKDF2
  doğrulaması yaptığını, DB yazım davranışını ve work factor sınırlarını
  kanıtlayan deterministik sözleşme testleri yazılır.

## Out of scope

- Lockout davranışı, role/permission, device session, password reset, MFA,
  users tablosu şeması veya login akışının davranış değişikliği.

## Dependencies

- V0-ARC-002

## Deliverables

- `docs/engineering/login-timing-contract.md` güvenlik sözleşmesi.
- AuthenticationService/PasswordHasher üzerinde sözleşmeyi uygulayan
  değişiklikler ve deterministik sözleşme testleri.
- Komut, exit code ve sonuç içeren kanıt kaydı.

## Acceptance evidence

- `dotnet test ALKAROS.slnx` tam çözüm üç ardışık koşuda exit code `0`
  verir (zamanlama testi dahil flake'siz).
- Sözleşme testleri deterministik geçer; stopwatch eşik testi kaldırılmıştır.
- Tam çözüm test transcript'i `evidence/V1-IAM-005/**` altında kayıtlıdır.

## Handoff

- V1-IAM-002
- V1-IAM-003

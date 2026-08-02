# V1-IAM-004 dogrulama kaydi

- Task ID: `V1-IAM-004`
- Tarih: 2026-08-02
- Sonuc: Basarili

## Uygulanan duzeltme

Failed-login sayaci PostgreSQL tarafinda tek `UPDATE ... RETURNING` ile
artirilir. Aktif lock varken yeni increment veya success reset uygulanmaz.

## Kontroller

- `dotnet test tests/Modules/Identity/Authentication/ALKAROS.Identity.Authentication.Tests.csproj --nologo`
  - Exit code: `0`
  - Sonuc: `34 passed, 0 failed`
- Paralel store ve service testleri 12 yanlis giriste sayacin besi asmadan
  lockout esigini korudugunu dogrular.

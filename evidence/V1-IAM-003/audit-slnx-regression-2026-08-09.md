# V1-IAM audit — slnx Authentication registro düzeltmesi (2026-08-09)

## Bulgu (kaynaktan bağımsız denetim)

`feat(iam-003)` commit'i (`7501108`) `ALKAROS.slnx` edit'i sırasında
`tests/Modules/Identity/Authentication/ALKAROS.Identity.Authentication.Tests.csproj`
kayıt satırını sessizce silmişti. Sonuç:

- `dotnet test ALKAROS.slnx` Authentication test projesini çalıştırmıyordu
  (51/51 test çözüm koşusuna katılmıyordu).
- V1-IAM-005 kabul kanıtı "tam çözüm üç ardışık koşu exit 0" bütünlüğü
  geçersiz duruma düşmüştü.

## Düzeltme

- `ALKAROS.slnx` içine Authentication.Tests.csproj satırı geri eklendi.
- Doğrulama: `dotnet sln list` 12/12 test projesi; tam çözüm koşusu
  **12/12 proje, 418 test, hepsi yeşil, EXIT=0** (Authentication.dll 51/51).

## Kapsam notu

- Bakım uzunluğu: `ALKAROS.slnx` kullanıcı onaylı cross-cutting kayıt
  setindedir (V1-IAM-002/003 closure); düzeltme o set içindedir.
- Bu dosya bağımsız denetim kaydı olarak `evidence/V1-IAM-003/**` altında
  saklanır (bulgu V1-IAM-003 commit'inde ortaya çıktı).
# V1-RMD-394 - Veri değiştiren endpoint'lerde idempotency anahtarı testi

- Task ID: V1-RMD-394
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-28

## Goal

Veri değiştiren (POST, PUT, PATCH, DELETE) her endpoint'in bir idempotency anahtarı zorunlu tuttuğunu denetleyen bir mimari test eklenir. `docs/architecture/api-contract-standard.md` bunu tekrar denemeye karşı güvence olarak şart koşuyor; bugün makine denetimi yok.

## Owned surface

- `tests/Architecture/ApiConventions/MutatingEndpointIdempotencyTests.cs`
- `tests/Architecture/ApiConventions/idempotency_allowlist.json`
- `plan/v1/remediation/V1-RMD-394-mutating-endpoint-idempotency-test.md`

## In scope

1. Test, veri değiştiren her endpoint'in istek sözleşmesinde bir `IdempotencyKey` alanı veya `X-Idempotency-Key` başlığı bulunduğunu denetler (kod bugün gövdeyi kullanıyor, standart başlığı yazıyor; iki biçim de kabul edilir).
2. Bugün uymayan her endpoint gerekçesi ve sorumlu görev kimliğiyle `idempotency_allowlist.json` dosyasına yazılır; liste yalnız küçülebilir.

## Out of scope

- Eksik anahtarların eklenmesi; her modül için ayrı görev açılır.

## Dependencies

- V1-RMD-393

## Acceptance evidence

- `dotnet test tests/Architecture/ApiConventions` exit code 0 verir.
- Mutasyon kontrolü: anahtarsız yeni bir POST endpoint'i test kırmızı yapar, geri alınınca yeşile döner; çıktı `evidence/V1-RMD-394/` altına kaydedilir.
- Semih için senaryo: aynı ödeme isteği iki kez gönderildiğinde ikinci istek yeni kayıt yaratmaz; bu davranışın artık her yeni endpoint için zorunlu olduğu testte görülür.

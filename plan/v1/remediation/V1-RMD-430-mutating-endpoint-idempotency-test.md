# V1-RMD-430 - Veri değiştiren endpoint'lerde idempotency anahtarı testi

- Task ID: V1-RMD-430
- Status: Done
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-28

## Goal

Veri değiştiren (POST, PUT, PATCH, DELETE) her endpoint'in bir idempotency anahtarı zorunlu tuttuğunu denetleyen bir mimari test eklenir. `docs/architecture/api-contract-standard.md` bunu tekrar denemeye karşı güvence olarak şart koşuyor; bugün makine denetimi yok.

## Owned surface

- `tests/Architecture/ApiConventions/MutatingEndpointIdempotencyTests.cs`
- `tests/Architecture/ApiConventions/idempotency_allowlist.json`
- `plan/v1/remediation/V1-RMD-430-mutating-endpoint-idempotency-test.md`

## In scope

1. Test, veri değiştiren her endpoint'in istek sözleşmesinde bir `IdempotencyKey` alanı veya `X-Idempotency-Key` başlığı bulunduğunu denetler (kod bugün gövdeyi kullanıyor, standart başlığı yazıyor; iki biçim de kabul edilir).
2. Bugün uymayan her endpoint gerekçesi ve sorumlu görev kimliğiyle `idempotency_allowlist.json` dosyasına yazılır; liste yalnız küçülebilir.

## Out of scope

- Eksik anahtarların eklenmesi; her modül için ayrı görev açılır.

## Dependencies

- V1-RMD-429

## Acceptance evidence

- `dotnet test tests/Architecture/ApiConventions` exit code 0 (5/5, gerçek Host ve PostgreSQL 18):
  `evidence/V1-RMD-430/tests.log`.
- Test, çalışan Host'un uç listesini okur; POST, PUT, PATCH ve DELETE uçlarında gövdede `IdempotencyKey` alanı ya da
  `X-Idempotency-Key` / `Idempotency-Key` başlık parametresi arar. Bugün anahtarsız 191 uç (149 POST, 26 PUT,
  14 DELETE, 2 PATCH) `idempotency_allowlist.json` içinde gerekçe ve ucu tanımlayan dosyanın plan sahibi görevle
  kayıtlı; anahtar eklenen ya da kaldırılan bir uç listede kalırsa test kırılır.
- Mutasyon kontrolü: anahtarsız geçici bir POST ucu testi kırmızı yapar; aynı uca gövde anahtarı ya da
  `X-Idempotency-Key` başlığı eklenince yeşile döner; uç kaldırılınca yeşil (`evidence/V1-RMD-430/mutation.log`).
- Semih için senaryo: yeni bir veri değiştiren uç tekrar anahtarı olmadan eklenirse bu test hangi ucun eksik
  olduğunu söyleyerek kırılır; listedeki uçlara anahtar eklendikçe liste kısalır.

# V1-RMD-429 - API rota ve yetki kuralı için mimari test

- Task ID: V1-RMD-429
- Status: InProgress
- Assignee: claude-code-session_012a6DqV4367gGp1Uk8TUam1
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-28

## Goal

Host'un yayınladığı her HTTP endpoint'inin `/api/v1` altında olduğunu ve bir yetki kararından geçtiğini makineyle denetleyen bir mimari test eklenir. Bugün bu kural yalnız `docs/architecture/api-contract-standard.md` içinde yazılı; kod bu kuralı ihlal ederse hiçbir test kırılmıyor.

## Owned surface

- `tests/Architecture/ApiConventions/ALKAROS.Architecture.ApiConventions.Tests.csproj`
- `tests/Architecture/ApiConventions/packages.lock.json`
- `tests/Architecture/ApiConventions/ApiRouteAuthorizationTests.cs`
- `tests/Architecture/ApiConventions/api_convention_allowlist.json`
- `plan/v1/remediation/V1-RMD-429-api-route-authorization-architecture-test.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx

## In scope

1. Yeni test projesi Host'un çalışma zamanındaki gerçek endpoint listesini okur (kaynak metni taramaz) ve her `RouteEndpoint` için rotanın `/api/v1` ile başladığını denetler.
2. Aynı test her endpoint'in bir yetki kararından geçtiğini (grup filtresi, izin kontrolü veya açık anonim işaret) denetler.
3. Bugün kurala uymayan her endpoint, gerekçesi ve sorumlu görev kimliğiyle `tests/Architecture/ApiConventions/api_convention_allowlist.json` dosyasına yazılır. Liste yalnız küçülebilir: artık ihlal etmeyen bir kayıt kalırsa test kırılır (`tools/consistency-audit/unreachable_services_allowlist.json` ile aynı mandal kalıbı).

## Out of scope

- Mevcut ihlallerin düzeltilmesi; her biri ayrı görevle kapatılır.

## Dependencies

- None

## Acceptance evidence

- `dotnet build ALKAROS.slnx` ve `dotnet test tests/Architecture/ApiConventions` exit code 0 verir.
- Mutasyon kontrolü: Host'a geçici olarak `/api/v1` dışında yetkisiz bir endpoint eklenince test kırmızı olur, geri alınınca yeşile döner; çıktı `evidence/V1-RMD-429/` altına kaydedilir.
- Semih için senaryo: Claude'dan "yetkisiz bir endpoint ekle" istenir ve commit denemesinin testte durduğu görülür.

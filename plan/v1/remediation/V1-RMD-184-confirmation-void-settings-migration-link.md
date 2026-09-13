# V1-RMD-184 - Confirmation/Void test projelerine eksik settings göçü

- Task ID: V1-RMD-184
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

2026-09-12 tarihli beş-ajanlı bağımsız Garson audit'inin bulgusunu
kapatır — bu oturumda beş kez daha görülmüş aynı sınıftan bir hata:
`tests/Host/Experience/Orders/Confirmation` ve
`tests/Host/Experience/Orders/Void` test projeleri, `026-typed-
settings.up.sql`'i (`settings.settings`/`settings.setting_history`)
kendi `.csproj`'larına bağlamamıştı. Bu iki proje, Host'u gerçek bir
`WebApplicationFactory` ile ayağa kaldırıyor; Host'la birlikte başlayan
`QrOrderExpiryHostedService` (arka plan döngüsü, `ISettingsService`
üzerinden `QrOrderExpirySetting`'i okuyor) her turda
`42P01: relation "settings.settings" does not exist` ile
başarısız oluyor, kendi `LogLoopFault`'ıyla sessizce yutuluyordu —
hiçbir testi kırmıyor, ama arka planda gerçek bir hata sürekli
tekrarlanıyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-184-confirmation-void-settings-migration-link.md` (yeni)
- Sınırlı ek:
  - tests/Host/Experience/Orders/Confirmation/ALKAROS.Host.Experience.Orders.Confirmation.Tests.csproj
    (Host test sahipliğinde) — `026-typed-settings.up.sql` bağlandı.
  - tests/Host/Experience/Orders/Void/ALKAROS.Host.Experience.Orders.Void.Tests.csproj
    (Host test sahipliğinde) — aynı ekleme.

## Out of scope

- Yok — bu iki test projesinin dışında aynı eksiklik başka bir yerde
  yok (bu oturumdan önceki beş tekrarı zaten ayrı görevlerle kapandı).

## Dependencies

- V1-SET-001

## Acceptance evidence

- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test`:
  - `tests/Host/Experience/Orders/Confirmation/*.csproj` → 19/19 yeşil
    (değişiklikten önce de 19/19'du — bu hosted-service hatası hiçbir
    testi kırmıyordu, gövde bu görevin kapattığı asıl şey).
  - `tests/Host/Experience/Orders/Void/*.csproj` → 5/5 yeşil.
- Doğrudan log yakalama bu iki proje için ayrı bir altyapı gerektirdiği
  için yapılmadı; düzeltme, bu oturumda beş kez uygulanmış aynı
  sınıftan göç-bağlama düzeltmesinin birebir aynısı (migration
  kendi kendine yeten, başka bir göçe bağımlı değil — doğrudan
  `026-typed-settings.up.sql`'in içeriğinden doğrulandı).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal.

## Handoff

- None

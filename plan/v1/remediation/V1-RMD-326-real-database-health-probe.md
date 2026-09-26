# V1-RMD-326 - Sağlık kontrolleri tamamen kendinden-bildirimdi, gerçek prob yoktu

- Task ID: V1-RMD-326
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) K14 bulgusu: `POST /health-checks` istemciden gelen `Status`'u doğrudan kaydediyor, gerçek DB/disk/dış servis pingleyen hiçbir `BackgroundService` yoktu — sağlık durumu tamamen çağıranın iddiasına dayanıyordu. `BackupHealthService.CaptureSystemHealthSnapshotAsync` da hiçbir yerden çağrılmıyordu (ayrı, bu görevin kapsamı dışında bırakılan bir gözlem).

## Owned surface

- `plan/v1/remediation/V1-RMD-326-real-database-health-probe.md`
- `src/Host/Experience/Observability/DatabaseHealthProbeHostedService.cs` (yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Observability/DatabaseHealthProbeHostedServiceTests.cs
  (yeni dosya, klasör V1-RMD-251 tarafından zaten sahiplenilmiş)

## In scope

1. `DatabaseHealthProbeHostedService` (yeni `BackgroundService`): dakikada bir gerçek bir Postgres round-trip'i (`SELECT 1`) yapar, sonucu `IObservabilityService.RecordHealthCheckAsync` üzerinden AYNI yoldan kaydeder (bir HTTP çağıranının kullandığı yolun tıpkısı) — böylece `GET /health-checks/unhealthy` ve `/by-target`'ta gerçekten görünür.
2. Bilinçli olarak dar kapsam: yalnız veritabanı bağlantısı (en temel, yük taşıyan bağımlılık) — disk/dış servis probu bu görevin kapsamı dışı, ayrı bulgular olarak bırakıldı.

## Out of scope

- Disk alanı / dış servis (QNB, Token, Yemeksepeti) probları — ayrı, daha büyük görevler gerektirir.
- `BackupHealthService.CaptureSystemHealthSnapshotAsync`'in gerçekten bir yerden çağrılması — bu görevle ilgisiz, ayrı bir bulgu.
- `HealthCheckController`/POST endpoint'inin kendisi hâlâ istemciden gelen iddiaları da kabul ediyor (kasıtlı — üçüncü taraf/manuel operasyonel kontroller için gerçek bir kullanım alanı var); bu görev yalnız EN AZ BİR gerçek, bağımsız kaynağın var olmasını sağladı.

## Dependencies

- None

## Acceptance evidence

Host testleri (UTF8 Postgres 18), gerçek bir veritabanına ve GERÇEKTEN kırık bir bağlantıya karşı:

- `AProbeAgainstARealHealthyDatabaseRecordsHealthy`: gerçek test veritabanına karşı `ProbeAsync()` çağrılır, `GetLatestHealthChecksByTargetAsync("postgres")` gerçekten `Healthy` bir kayıt döndürür.
- `AProbeAgainstAnUnreachableDatabaseRecordsUnhealthyInsteadOfThrowing`: hiçbir şeyin dinlemediği bir porta (`127.0.0.1:1`) işaret eden GERÇEK, kasıtlı olarak kırık bir `NpgsqlDataSource` ile — sahte bir istisna değil, gerçek bir bağlantı hatası — `ProbeAsync()` fırlatmaz, `Unhealthy` kaydeder.

`ALKAROS.Host.Experience.Observability.Tests` 8/8 (2 yeni). `ALKAROS.Host.Experience.Composition.Tests` 10/10 — bu paketin kasıtlı olarak bağlanamayan sahte `NpgsqlDataSource`'u, `BackgroundService`'in (K7'nin `IHostedService`'iyle karıştırılmaması gereken) `ExecuteAsync`'i Host başlangıcını hiç bloklamadığı için tüm paketi bozmadı — bu ayrım bilerek doğrulandı.

Mutasyon kontrolü: `RunProbeAsync`'in gerçek DB kontrolü geçici olarak devre dışı bırakılıp her zaman `Healthy` döndürecek şekilde değiştirildi — yeni "unhealthy" testi gerçekten kırmızı oldu (`Expected: Unhealthy, Actual: Healthy`); dosya `diff` ile birebir orijinaline geri getirildi, tüm paket tekrar yeşil.

## Handoff

- None

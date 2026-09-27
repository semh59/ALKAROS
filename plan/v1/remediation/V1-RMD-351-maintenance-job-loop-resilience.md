# V1-RMD-351 - Bakım işi döngüsü artık diğer 9 hosted service'le aynı hata toleransı desenini kullanıyor

- Task ID: V1-RMD-351
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) düşük seviye bulgusu: "`MaintenanceJobHostedService`'in diğer 7
hosted service'ten farklı hata toleransı deseni." Doğrulandı: `MaintenanceJobHostedService.LoopAsync` yalnızca
`OperationCanceledException`'ı (kapatma sinyali) yakalıyordu — `_runner.RunAsync(...)`'in KENDİ iş yürütme
hatalarını zaten içeride yakaladığı (kaydedip "Failed" olarak işaretlediği, asla yeniden fırlatmadığı) doğrulandı,
ama runner'ın KENDİ çevresindeki iskelet kodunda (kilit sözlüğü erişimi, durum arama) beklenmedik bir hata
oluşsaydı, bu `LoopAsync`'in dışına sızar, `ExecuteAsync`'in `Task.WhenAll`'ı BAŞARISIZ olur ve `BackgroundService`
bu görevi asla yeniden başlatmadığından TÜM bakım işleri (yalnızca sorunlu olan değil) sürecin geri kalanı boyunca
sessizce durabilirdi. Bu, aynı oturumun K14 bulgusunda (`DatabaseHealthProbeHostedService`) ve
`LowStockAlertHostedService`'te zaten kurulu, doğrulanmış deseni (her turu geniş bir `catch (Exception)` ile
sarmalayıp loglamak, döngüyü hiç durdurmamak) izlemiyordu.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/Maintenance/MaintenanceJobs.cs
- `plan/v1/remediation/V1-RMD-351-maintenance-job-loop-resilience.md`

## In scope

1. `LoopAsync`: `_runner.RunAsync(...)` çağrısı artık kendi `try/catch`'i içinde — `OperationCanceledException`
   (gerçek kapatma) döngüyü düzgünce sonlandırıyor, BAŞKA HERHANGİ bir istisna loglanıp döngü BİR SONRAKİ
   aralıkta devam ediyor — `DatabaseHealthProbeHostedService`'in (K14) kendi, zaten test edilmiş deseniyle
   birebir aynı şekilde.
2. Yeni `ILogger<MaintenanceJobHostedService>` bağımlılığı (DI zaten `AddHostedService<...>` ile otomatik
   çözüyor, kayıt değişikliği gerekmedi).

## Out of scope

1. Bu görevin hedeflediği DAR senaryonun (runner'ın kendi çevresindeki iskelet kodunun — kilit sözlüğü erişimi
   gibi — beklenmedik şekilde fırlatması) kendine özgü, izole bir otomatik testi. `MaintenanceJobRunner`
   `sealed`, bir arayüz arkasında değil, ve iş yürütme hatalarının TAMAMI zaten kendi `try/catch`'i içinde
   güvenle yakalanıyor (doğrulandı) — bu yüzden bu DAR senaryoyu gerçekçi bir şekilde tetiklemek, ya
   yansıma (reflection) tabanlı bir hata enjeksiyonu ya da `MaintenanceJobHostedService`/`MaintenanceJobRunner`'ı
   enjekte edilebilir bir soyutlama arkasına almak için bir yeniden yapılandırma gerektirir — düşük seviyeli bu
   bulgunun kendi kapsamının ötesinde, orantısız bir değişiklik. Düzeltmenin kendisi K14'ün ZATEN doğrulanmış,
   test edilmiş desenin mekanik bir kopyası olduğu için düşük risk taşıyor.

## Dependencies

- None

## Acceptance evidence

- `tests/Host/Experience/SecurityAdministration/ALKAROS.Host.Experience.SecurityAdministration.Tests.csproj`:
  27/27 test geçti (regresyon yok) — bu paket `MaintenanceJobRunner`'ı gerçek HTTP uç noktaları üzerinden
  (`/maintenance/jobs`, `/maintenance/jobs/{name}/run`) zaten kapsıyor; yapıcıya eklenen `ILogger` parametresi
  hiçbir mevcut davranışı bozmadı.
- `dotnet build src/Host/ALKAROS.Host.csproj`: sıfır hata, sıfır uyarı.
- Düzeltmenin kendisi, aynı oturumun K14 bulgusunda (`DatabaseHealthProbeHostedService`, gerçek Postgres'e karşı
  test edilmiş) zaten doğrulanmış olan AYNI desenin (per-tur geniş `catch`, logla, devam et) satır satır mekanik
  bir kopyası — kod incelemesiyle karşılaştırıldı.

## Handoff

- None

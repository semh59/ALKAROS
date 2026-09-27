# V1-RMD-353 - Relay bağlantısı durum okuması artık gerçek async ADO.NET kullanıyor

- Task ID: V1-RMD-353
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) düşük seviye bulgusu: `PostgresRelayConnectorStatusReporter.cs`
(satır 35-59 civarı) senkron ADO.NET çağrıları (`OpenConnection`/`ExecuteReader`) kullanıyordu. Dosyanın kendi
önceki yorumu bunu bilinçli bir tasarım olarak savunuyordu ("genuinely synchronous ADO.NET calls ... rather
than this reporter wrapping async work in a blocking anti-pattern"), ancak bu gerekçe bu reporter'ın TEK gerçek
çağıranını (`RelaySettingsEndpoints.cs`'in `/status` uç noktası) atlamıştı: o uç nokta ZATEN `async` bir istek
işleyicisi, ve içindeki gerçekten senkron bir DB round-trip, kaçınmaya çalıştığı sync-over-async anti-desenine
aynı maliyeti (bir thread-pool iş parçacığını DB round-trip süresi boyunca bloke etmek) taşıyor.
`IRelayConnectorStatusReporter` arayüzü, bu reporter'ın (Postgres okuması) YANINDA `RelayConnectorSupervisor`
tarafından da (gerçek işlem içi, anında bellek okuması) uygulanıyor — bu ikincisi için senkron kalması hâlâ
tamamen doğru, o yüzden değişiklik arayüzü async'e çevirip her iki tarafı da güncelledi.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Integrations/QrRelay/LocalConnector/RelayConnectorStatus.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Integrations/QrRelay/LocalConnector/RelayConnectorSupervisor.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Integrations/QrRelay/LocalConnector/RelayConnectorStatusPublisher.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Integrations/QrRelay/PublicGateway/PostgresRelayConnectorStatusReporter.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/RelaySettings/RelaySettingsEndpoints.cs
- `plan/v1/remediation/V1-RMD-353-relay-connector-status-reporter-async.md`

## In scope

1. `IRelayConnectorStatusReporter.CurrentStatus { get; }` → `Task<RelayConnectorStatus> GetCurrentStatusAsync(CancellationToken)`.
2. `RelayConnectorSupervisor`: kendi senkron `CurrentStatus` özelliği (üç iç kullanım yeri değişmedi) korunuyor;
   arayüzü uygulayan yeni `GetCurrentStatusAsync` bunu `Task.FromResult(CurrentStatus)` ile sarmalıyor.
3. `RelayConnectorStatusPublisher.PublishAsync`: çağrı `await _statusReporter.GetCurrentStatusAsync(cancellationToken)`
   oldu (zaten `async` bir metot içindeydi, davranış değişmedi).
4. `PostgresRelayConnectorStatusReporter`: özellik tamamen silindi, yerine `OpenConnectionAsync`/
   `ExecuteReaderAsync`/`ReadAsync` kullanan gerçek async bir metot — bu depodaki HER ÖTEKİ Postgres okumasıyla
   aynı desen.
5. `RelaySettingsEndpoints.cs`'in `/status` uç noktası: `await connectorStatus.GetCurrentStatusAsync(cancellationToken)`.
6. İlgili testler (`PostgresRelayConnectorStatusReporterTests.cs`, `RelayConnectorStatusPublisherTests.cs`'in
   `FakeStatusReporter`'ı) yeni async imzaya güncellendi.

## Out of scope

1. `RelayConnectorSupervisor.CurrentStatus`'un kendisini async'e çevirmek — o okumanın işlem içi, anında ve
   I/O içermeyen doğası göz önüne alındığında gerçek bir fayda sağlamaz; yalnızca kendi üç iç çağrı yerini
   (satır 159, 166, 207) gereksiz yere `await`'lemeye zorlardı.

## Dependencies

- None

## Acceptance evidence

- `dotnet build src/Host/ALKAROS.Host.csproj`: 0 hata, 0 uyarı.
- `tests/Integrations/QrRelay/PublicGateway/ALKAROS.QrRelay.PublicGateway.Tests.csproj`: 26/26 geçti (gerçek
  test Postgres'ine karşı, `alkaros-test-pg` port 55432).
- `tests/Integrations/QrRelay/LocalConnector/ALKAROS.QrRelay.LocalConnector.Tests.csproj`: 9/9 geçti (gerçek
  test Postgres'ine karşı) — `RelayConnectorStatusPublisher`'ın `FakeStatusReporter` üzerinden yeni async
  arayüzü doğru çağırdığını doğruluyor.
- `tests/Host/Experience/RelaySettings/ALKAROS.Host.Experience.RelaySettings.Tests.csproj`: 9/9 geçti (gerçek
  test Postgres'ine karşı, gerçek HTTP round-trip) — `/status` uç noktasının yanıtı değişmedi, davranış
  regresyonu yok.
- Mutation-check gerekçesi: bu değişiklik bir imza refactor'ı (senkron özellik → async metot); eski imzayı
  geri getirmek yeni test kodunu (artık `await ...GetCurrentStatusAsync(...)` çağıran) derleme hatasıyla
  başarısız kılar — yapısal bir refactor için geçerli bir "red" sinyali. Asıl davranışsal iyileştirme (gerçek
  async I/O ile bir thread-pool iş parçacığının artık bloke olmaması), Postgres'in senkron ve async ADO.NET
  API'leri arasındaki farkın kendisi olduğundan, uygulamanın doğruluğu kod incelemesiyle (bu depodaki HER
  DİĞER async Postgres okumasıyla birebir aynı, zaten kanıtlanmış desen) ve üç test paketinin tamamının gerçek
  bir Postgres'e karşı hâlâ yeşil kalmasıyla doğrulandı.

## Handoff

- None

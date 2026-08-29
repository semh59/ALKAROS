# V1-RMD-008 - Host API authorization, rate limit partitioning, TLS proxy and telemetry

- Task ID: V1-RMD-008
- Status: Done
- Assignee: /root/rmd008_host_security
- Work type: implementation
- Surface state: Existing

## Goal

Dual-screen Host API yüzeyinde client IP/terminal bazlı rate limit bölümlemesi, reverse proxy TLS forwarded headers
uyumu, unhandled 500 exception loglaması ve Retry-After yanıt başlıklarını eklemek.

## Owned surface

- `src/Host/DualScreen/DualScreenOptions.cs`
- `tests/Host/MigrationComposition/DualScreen/CustomerDisplayContractTests.cs`
- `tests/Host/MigrationComposition/DualScreen/DualScreenOptionsTests.cs`
- `evidence/V1-RMD-008/**`

V1-GOV-008 custody correction: V1-RMD-008'in tamamlanmış güvenlik davranışı ve tarihsel uygulama kanıtı korunur;
`src/Host/DualScreen/DualScreenApplication.cs` ile
`tests/Host/MigrationComposition/DualScreen/DualScreenHostTests.cs` üzerindeki sonraki remediation custody'si yalnız
katalog HTTP sayfalama sözleşmesi için V1-RMD-009'a devredilmiştir.

## In scope

- `DualScreenApplication.cs` rate limiter konfigürasyonunu istemci IP ve terminal bazlı bölümlemek.
- 429 Too Many Requests yanıtına `Retry-After` HTTP başlığı ve hata süresi eklemek.
- Ters vekil arkasında `ForwardedHeadersOptions` ile `X-Forwarded-Proto` desteği eklemek; düz HTTP isteklerinde
  güvenli davranış sağlamak.
- Yakalanan 500 hatalarını yapılandırılmış loglamak.
- İlgili uç nokta yetkilendirme ve proxy entegrasyon testlerini yazmak.
- Authenticated fakat mevcut IAM izni olmayan cashier mutation çağrılarını `403` ile reddetmek ve denial kaydını
  doğrulamak; display DTO allowlist'ini personel, token, secret ve dahili not alanlarına karşı korumak.

## Out of scope

- PosTerminal frontend kodunu değiştirmek.
- Veritabanı şemasını değiştirmek.

## Dependencies

- V1-RMD-007

## Deliverables

- Bölümlenmiş rate limiter ve güvenli proxy middleware içeren uygulama paketi.
- Loglanabilir hata yönetimi ve Retry-After desteği.
- Çoklu istemci rate limit ve proxy TLS doğrulama testleri.

## Acceptance evidence

- Trusted proxy allowlist dışındaki `X-Forwarded-*` başlıkları scheme/client IP değiştiremez; production dışı loopback
  development haricinde plain HTTP login ve mutation fail-closed olur, secure cookie davranışı test edilir.
- Login/pairing/mutation kotaları uygun principal, terminal/display ve doğrudan/trusted client IP anahtarlarıyla ayrılır;
  spoof edilmiş forwarding ile kota atlanamaz ve `429` lease metadata kaynaklı `Retry-After` taşır.
- Authenticated fakat izinsiz mutasyon `403`, display mutasyonu `403`, expired/revoked principal `401` verir; gerçek HTTP
  integration testleri response-started dahil her 500 yolunda structured error log ve trace ID'yi doğrular.
- `dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj` exit code 0 verir.
- `evidence/V1-RMD-008/**` altında HTTP test transkriptleri saklanır.

## Handoff

- V1-RMD-009

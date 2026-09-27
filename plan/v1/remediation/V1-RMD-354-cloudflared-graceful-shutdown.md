# V1-RMD-354 - cloudflared artık önce SIGTERM ile durduruluyor, yalnızca zaman aşımında sert kill'e düşüyor

- Task ID: V1-RMD-354
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) düşük seviye bulgusu: `cloudflared` süreci, düzgün bir kapatma
denemesi olmadan doğrudan `Process.Kill()` (Linux konteynerinde SIGKILL) ile sonlandırılıyordu.
`ICloudflaredProcess.RequestStop()`'un kendi belge yorumu "requests a graceful stop" diyordu, ama uygulama bunu
hiç denemiyordu. `cloudflared` kendi SIGTERM işleyicisiyle Cloudflare edge'e olan QUIC/HTTP2 bağlantılarını
düzgünce kapatır; SIGKILL bu şansı hiç vermez — edge, bağlantının gittiğini yalnızca kendi liveness zaman
aşımından sonra fark eder, anında değil. `docker stop`'un kendi deseni (SIGTERM, bekle, sonra SIGKILL) izlendi.

## Owned surface

- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Integrations/QrRelay/LocalConnector/CloudflaredProcessFactory.cs
- `plan/v1/remediation/V1-RMD-354-cloudflared-graceful-shutdown.md`

## In scope

1. `RealCloudflaredProcess.RequestStop()`: Linux'ta (`OperatingSystem.IsLinux()` — analizörün tanıdığı platform
   koruması) önce `libc`'nin `kill(2)`'sine P/Invoke ile SIGTERM gönderiyor, süreç en fazla 5 saniye (
   `GracefulShutdownTimeout`) boyunca çıkış yapmasını bekliyor (50ms aralıklarla `HasExited` kontrolü); süreç bu
   sürede kapanmazsa (ya da Linux dışı bir platformdaysa, ya da `libc`'ye P/Invoke başarısız olursa) mevcut
   `Process.Kill(entireProcessTree: true)` yoluna düşüyor — davranış hiçbir zaman "asla durmuyor" olmuyor,
   sadece önce nazik bir şans veriyor.
2. Windows (bu depronun geliştirme/test makinesi) davranışı BİLİNÇLİ OLARAK değişmedi — Windows'ta rastgele bir
   konsol sürecine SIGTERM eşdeğeri bir sinyal göndermenin (AttachConsole/GenerateConsoleCtrlEvent'in aynı
   konsola bağlı olmayan bir sürece güvenilir şekilde çalışmaması) standart, karmaşık olmayan bir yolu yok;
   üretimde bu kod SADECE Linux konteynerinde çalışıyor (`deploy/docker/Dockerfile`), bu yüzden Windows dalı
   eski, zaten test edilmiş davranışını (doğrudan Kill) koruyor.

## Out of scope

1. `RealCloudflaredProcess`'in kendisi için özel bir otomatik test paketi — gerçek `cloudflared` ikilisini (veya
   herhangi bir gerçek alt süreci Linux'a özgü SIGTERM semantiğiyle) gerektiriyor, bu Windows geliştirme/CI
   makinesinde pratik olarak inşa edilemez. Bunun yerine gerçek Linux doğrulaması, bu düzeltmenin TAM AYNI
   mantığını (P/Invoke imzası, zaman aşımlı bekleme döngüsü, sert-kill'e düşüş) birebir kopyalayan bağımsız,
   tek seferlik bir doğrulama programıyla, gerçek bir Linux Docker konteyneri (`mcr.microsoft.com/dotnet/sdk:8.0`)
   içinde gerçek `bash` alt süreçlerine karşı çalıştırılarak yapıldı (aşağıya bakınız); program ve konteyner
   sonrasında temizlendi.

## Dependencies

- None

## Acceptance evidence

- `dotnet build src/Integrations/QrRelay/ALKAROS.QrRelay.csproj`: 0 hata, 0 uyarı (platform-uyumluluk analizörü
  `OperatingSystem.IsLinux()` korumasını tanıdı, `CA1416` tetiklenmedi).
- `tests/Integrations/QrRelay/LocalConnector/ALKAROS.QrRelay.LocalConnector.Tests.csproj`: 9/9 geçti (regresyon
  yok — bu paketteki testler `ICloudflaredProcess`'in bir Fake'ini kullanıyor, `RealCloudflaredProcess`'e
  dokunmuyor).
- **Gerçek Linux doğrulaması** (`mcr.microsoft.com/dotnet/sdk:8.0` konteyneri, bağımsız, atılabilir bir .NET
  konsol programıyla, `RequestStop`'un birebir aynı mantığının kopyası):
  - Senaryo 1 (SIGTERM'i yakalayıp temiz çıkan bir `bash` alt süreci): yeni kod çalıştırıldı, süreç 116ms içinde
    çıktı VE trap'in kendi işaret dosyasını yazdığı doğrulandı (`SCENARIO1_EXITED=True SCENARIO1_MARKER=True`)
    — SIGTERM'in gerçekten iletildiğini ve sürecin düzgün kapanma şansı bulduğunu kanıtlıyor.
  - Senaryo 2 (SIGTERM'i tamamen yok sayan bir `bash` alt süreci): yeni kod, 2 saniyelik zaman aşımının
    ("ölçüldü: 2116ms) sonunda sert kill'e düşerek süreci hâlâ sonlandırdı (`SCENARIO2_EXITED=True`) — düzgün
    kapatma başarısız olduğunda geri dönüşün gerçekten çalıştığını kanıtlıyor.
  - Senaryo 3 (ESKİ kod — doğrudan `Kill()` — aynı SIGTERM-yakalayan script'e karşı): işaret dosyası HİÇ
    yazılmadı (`SCENARIO3_MARKER_ABSENT=True`) — düzeltmenin çözdüğü boşluğun gerçek olduğunu (SIGKILL, trap'e
    hiç şans vermiyor) doğrudan kanıtlıyor.
  - Doğrulama programı ve konteyner sonrasında temizlendi; depoya hiçbir kalıcı test dosyası eklenmedi (yalnızca
    `src/Integrations/QrRelay/LocalConnector/CloudflaredProcessFactory.cs` değişti).

## Handoff

- None

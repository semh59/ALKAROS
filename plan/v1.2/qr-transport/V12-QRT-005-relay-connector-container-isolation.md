# V12-QRT-005 - Relay connector'ı ayrı bir container'a taşımak

- Task ID: V12-QRT-005
- Status: Done
- Assignee: claude-code-session_01Xsqh6z1RYhmFapKkHKoBmk
- Work type: implementation
- Surface state: Existing

## Goal

`RelayConnectorSupervisor` (bir `BackgroundService`, V12-QRT-001), şu an
ALKAROS'un geri kalan HER modülünü (Orders, Billing, Payments, Cash, Kitchen
vb.) barındıran aynı `api` container'ı içinde, aynı process'te çalışıyor.
Kendi kod yorumu bu tasarım varsayımını açıkça belirtiyor: "Host process
zaten çalışıyor olmak zorunda (POS'un çalışması için), o yüzden tünel
connector'ı da onunla birlikte tutmak yeterli" — yani bilinçli olarak ayrı
bir yaşam döngüsü tasarlanmamış, birlikte çalışmanın gerçek bir maliyeti
olmadığı varsayılmış.

Bu varsayımın bugün gerçek bir maliyeti var: `cloudflared` (bu supervisor
tarafından başlatılıyor), bu sistemde internetten gerçekten erişilebilen TEK
bileşen (V0-ARC-009'un kendi topolojisi: Customer Phone → Public QR Relay →
Durable Queue → Local Outbound Connector → POS Backend). Aynı process'i
paylaşmak şu anlama geliyor: (1) ana API'nin rutin bir restart/deploy'u,
o deploy'la hiç ilgisi olmayan müşteri-yüzü QR sipariş yolunu da düşürüyor,
(2) tam tersi yönde de — public relay yüzeyindeki bir kaynak baskısı veya
olay, personel-yüzü POS ile aynı blast-radius'u paylaşıyor, kendi process'ine
hapsolmuyor.

Bu görev, `RelayConnectorSupervisor` + onun başlattığı `cloudflared` alt
process'ini kendi container'ına taşıyor.

**Uygulama sırasında bulunan gerçek bir sorun ve düzeltmesi (2026-09-17):**
İlk yaklaşım, `network_mode: "service:api"` ile `api` container'ının network
namespace'ini paylaşarak "Connector → POS: Internal localhost" satırını
harfiyen korumaya çalıştı. Gerçek `docker compose restart` ile test edilince
şu ortaya çıktı: connector'ı tek başına restart etmek `api`'yi etkilemiyordu
(✅), ama **`api`'yi restart etmek connector'ı da zorla restart ediyordu**
(❌) — çünkü Docker, network namespace sahibi container durup yeniden
başladığında o namespace'i paylaşan her container'ı da etkiliyor. Bu, bu
görevin çözmeye çalıştığı asıl sorunun (rutin bir API deploy'unun public QR
sipariş yolunu düşürmesi) YARISINI hiç çözmüyordu.

Kullanıcı talimatıyla ("sorun varsa düzeltmek gerekir") düzeltildi: connector
artık compose ağında TAM bağımsız bir container — `network_mode` yok.
`cloudflared`'ın tünel hedefi `http://localhost:5080` yerine `http://api:5080`
(compose servis DNS adı). Bu, V0-ARC-009'un onaylı topolojisindeki
"Connector → POS" satırının MEKANİZMASINI değiştiriyor (OS loopback →
compose bridge network), ama güven sınırını/protokolü/yetkilendirmeyi
değiştirmiyor (hâlâ public inbound port yok, hâlâ aynı fiziksel host, hâlâ
API key ile yetkilendirme). Bu yüzden V0-ARC-009'un kendi dosyasına
(`docs/architecture/qr-relay-topology.md`) tarihli, gerekçeli bir amendment
notu eklendi — karar SİLİNMEDİ, yalnız bu tek satır + bir açıklama eklendi.

**İkinci bulunan sorun ve düzeltmesi (aynı gün):** compose DNS'e geçiş,
`DualScreenApplication.cs`'in HTTPS-zorunlu middleware'ini kırdı — bu
middleware, `/health/ready` ve NFC/QR relay istekleri DIŞINDA HER isteğin
HTTPS olmasını zorunlu tutuyor, ve o istisna şu ana kadar YALNIZ gerçek bir
loopback bağlantısını (127.0.0.1/::1) kabul ediyordu ("nothing outside this
container can present itself as a loopback peer" — sahtecilik-yapılamaz
argümanı tam olarak buydu). Connector artık loopback değil, bridge-network
IP'si üzerinden geldiği için ilk testte TÜM gerçek müşteri QR/NFC trafiği
`400 HTTPS_REQUIRED` ile reddedilmeye başladı — bu, çözülmeye çalışılan
sorundan (rutin deploy public sipariş yolunu düşürüyor) daha kötü bir
sonuç (public sipariş yolu HİÇ çalışmıyor) olurdu. Bridge-network IP'sini
körlemesine güvenilir saymak da yanlış olurdu (sahtecilik-yapılamaz
argümanını zayıflatır). Kullanıcı onayıyla düzeltildi: `api`/`postgres`/
`connector`'ın ÜÇÜ birden (ve YALNIZ bunlar) üye olduğu, sabit alt ağlı,
dar kapsamlı yeni bir Docker ağı (`relay-internal`) eklendi;
`DualScreenApplication.cs`'in NFC-relay istisnası artık loopback VEYA bu
özel ağın CIDR'ı içindeki bir kaynak adresini kabul ediyor
(`DualScreenOptions.NfcTrustedNetworks`, yeni `--nfc-trusted-network` CLI
bayrağı) — aynı "sahtecilik-yapılamaz" güç seviyesinde (bu ağa yalnızca bu
üç container inşa zamanında eklenebilir), yalnız container ayrımını
bozmadan. Ayrıca: connector, `api` ile İKİ ağı (default + relay-internal)
paylaşırsa Docker'ın gömülü DNS'i "api" adını hangi ağdaki IP'ye
çözeceğine güvenilir şekilde karar veremiyor (gerçek testte doğrulandı) —
bu yüzden connector YALNIZ `relay-internal`'e bağlı, `postgres`'e erişimi
de aynı ağ üzerinden (postgres da bu ağa eklendi).

Gerçek `docker compose restart api` / `restart connector` ile her ikisi de
artık birbirini etkilemeden çalıştığı VE NFC-relay geçidinin gerçek bir
istekle (connector → api, `relay-internal` üzerinden) hâlâ 200 döndüğü
doğrulandı.

## Owned surface

Preflight sırasında iki karar netleşti (kod incelemesiyle doğrulandı):

1. **Giriş noktası:** kendi `Program.cs`'i olan yeni, bağımsız bir
   executable proje (`ConnectorHost`) — `ALKAROS.Host`'a bir CLI modu
   eklemek değil, çünkü bu, connector'ı yine aynı büyük binary'nin bir
   parçası yapardı (asıl amaca aykırı).
2. **Token erişimi:** connector kendi Postgres bağlantısını açıp
   `IRelayTunnelStore.ResolveTunnelTokenAsync`'i kendisi çağıracak.
   `PostgresRelayTunnelStore`'un kendi kod yorumu bunu zaten yasaklıyor:
   "Never exposed through an HTTP endpoint — callers are backend automation
   only." Tünel run-token'ı bir bearer credential; `api`'nin bunu HTTP
   üzerinden connector'a taşıması bu güvenlik sınırını ihlal eder. Bu yüzden
   connector kendi DB connection pool'unu açıyor (görev metninin ilk
   taslağındaki "DB'ye bağlanmasın" tercihi bu güvenlik kısıtıyla çelişiyordu
   — düzeltildi).

Bunun ortaya çıkardığı ek bir gereksinim: `api`'nin `GET .../relay-credential/status`
uç noktası, connector'ın canlı durumunu (`IRelayConnectorStatusReporter.CurrentStatus`)
şu an aynı process'teki `RelayConnectorSupervisor` singleton'ından okuyor.
Connector ayrı bir process olunca bu artık mümkün değil — connector kendi
durumunu Postgres'e yazacak, `api` da onu oradan okuyacak (yeni, küçük bir
tablo; ne HTTP ne de shared-memory).

- `database/migrations/V12/V12-QRT-005/**` (yeni — `qr_ordering.relay_connector_status`
  tek-satırlı tablo, `PostgresRelayTunnelStore`'un `relay_tunnel` tablosuyla
  aynı şema/desen).
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Integrations/QrRelay/ALKAROS.QrRelay.csproj
  (V12-QRT-001 sahipliğinde kalır) — SDK-style proje glob'u varsayılan olarak
  iç içe `ConnectorHost/` alt projesinin `.cs` dosyalarını da kendi
  derlemesine dahil ettiği için (MSBuild alt-proje klasörlerini otomatik
  hariç tutmuyor), yalnız bunu dışlayan bir `<Compile Remove>` satırı eklenir;
  başka hiçbir ayar değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Integrations/QrRelay/LocalConnector/**
  (V12-QRT-001 sahipliğinde kalır) — yalnız yeni `ConnectorHost/` alt klasörü
  (`ALKAROS.QrRelay.ConnectorHost.csproj`, `Program.cs`: `--db-url`/
  `ALKAROS_DB_PASSWORD` ayrıştırma, DI kaydı, `RelayConnectorSupervisor`'ı
  hosted service olarak başlatma) ve yeni `RelayConnectorStatusPublisher.cs`
  (`IRelayConnectorStatusReporter.CurrentStatus`'u periyodik olarak
  Postgres'e yazan ayrı bir `BackgroundService`) eklenir;
  `RelayConnectorSupervisor.cs`'in kendi mantığına dokunulmaz —
  `CloudflaredProcessFactory.cs`'deki "`api` container'ında" diyen kod
  yorumu, artık doğru olmayan bu varsayımı düzeltecek şekilde (davranış
  değişmeden) güncellenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Integrations/QrRelay/PublicGateway/**
  (V12-QRT-001 sahipliğinde kalır) — yalnız yeni
  `PostgresRelayConnectorStatusReporter.cs` (`IRelayConnectorStatusReporter`'ın
  `api` tarafındaki Postgres-okuyan implementasyonu) eklenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Integrations/QrRelay/LocalConnector/**,
  tests/Integrations/QrRelay/PublicGateway/** (V12-QRT-001 sahipliğinde
  kalır) — yalnız yukarıdaki iki yeni sınıfın kendi testleri eklenir; mevcut
  test dosyaları düzenlenmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/RelaySettings/RelaySettingsEndpoints.cs
  (V12-QRT-003 sahipliğinde kalır) — yalnız `RelayConnectorSupervisor`/
  `ICloudflaredProcessFactory`/`AddHostedService` kayıt satırları kaldırılır,
  `IRelayConnectorStatusReporter` kaydı `PostgresRelayConnectorStatusReporter`'a
  değiştirilir; `IRelayTunnelStore` kaydı ve HTTP uç noktalarının kendisi
  değişmez (GetInfoAsync/SaveAsync hâlâ `api`'de, sır decrypt etmiyorlar).
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Integrations/QrRelay/PublicGateway/ICloudflareApiClient.cs,
  src/Integrations/QrRelay/PublicGateway/RelayProvisioningService.cs
  (V12-QRT-001 sahipliğinde kalır) — `LocalOriginService` sabiti
  `http://localhost:5080`'den `http://api:5080`'e değişir (yukarıdaki
  "bulunan sorun" başlığına bakın); ilgili kod yorumları güncellenir.
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/architecture/qr-relay-topology.md
  (V0-ARC-009 sahipliğinde kalır, karar Done statüsünde kilitli) — yalnız
  "Connector → POS" satırı ("Internal localhost" → "Internal Docker network
  (compose service DNS)") ve tarihli, gerekçeli bir "2a. Amendment" bölümü
  eklenir; kararın geri kalanı, approver'ı, tarihi silinmez/değiştirilmez —
  kullanıcının açık talimatıyla (2026-09-17, "sorun varsa düzeltmek gerekir").
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenOptions.cs,
  src/Host/DualScreen/DualScreenApplication.cs (Host composition root
  sahipliğinde kalır) — yeni `NfcTrustedNetworks` alanı + `--nfc-trusted-network`
  CLI bayrağı eklenir; NFC-relay-istisnası koşulu bunu OR ile genişletir
  (mevcut loopback davranışı DEĞİŞMEZ, yalnız genişletilir). Yukarıdaki
  "ikinci bulunan sorun" nedeniyle, kullanıcı onayıyla.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/DualScreen/DualScreenOptionsTests.cs
  (Host composition root sahipliğinde kalır) — yalnız yeni
  `--nfc-trusted-network` testleri eklenir; mevcut testler değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Integrations/QrRelay/PublicGateway/RelayProvisioningServiceTests.cs
  (V12-QRT-001 sahipliğinde kalır) — `LocalOriginService` değeri değiştiği
  için tek bir mevcut assertion (`http://localhost:5080` → `http://api:5080`)
  güncellenir; testin geri kalanı değişmez.
- Sınırlı ek (paylaşılan, geri-tik olmadan): deploy/docker/Dockerfile (yeni
  bir build stage + runtime target, `cloudflared` ikili dosyası artık bu
  yeni target'a taşınır — `api` target'ının onu içermesine gerek kalmaz),
  compose.yaml (yeni, tam bağımsız servis, `depends_on: api`; yeni, dar
  kapsamlı `relay-internal` ağı — yalnız `api`/`postgres`/`connector` üye;
  `postgres`'in kendi servis bloğuna bu ağ eklenir) — mevcut `api`/`web`/
  `postgres`/`migrate`/`provision` servislerinin başka hiçbir ayarı
  değişmez.
- Kapsam genişletme onayı (2026-09-17 kullanıcı talimatı, V11-UNT-001/
  V13-CSH-001 emsaliyle aynı desen): yeni `ConnectorHost` projesinin
  `ALKAROS.slnx` ve `build/project-manifest.json` içine kaydı.
- Sınırlı ek (paylaşılan, geri-tik olmadan): Directory.Packages.props —
  yalnız `Microsoft.Extensions.Hosting` sürüm kaydı eklenir (aynı sürüm,
  V12-QRT-001'in zaten eklediği `Microsoft.Extensions.Hosting.Abstractions`
  ile eşleşir).
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs,
  tests/Host/MigrationComposition/Manifest/ManifestTests.cs — yeni migration
  pozisyonunun kaydı.

## In scope

- `ConnectorHost` adlı yeni, bağımsız executable: `--db-url`/
  `ALKAROS_DB_PASSWORD` ayrıştırma (ALKAROS.Host.Program'daki aynı desen,
  kod paylaşımı olmadan küçük bir kopya — çapraz-bağımlılık yaratmamak
  için), DI kaydı (Secrets/SensitiveData building block'ları
  `QrOrderingModule.Register`'daki ile birebir aynı), `RelayConnectorSupervisor`
  - yeni `RelayConnectorStatusPublisher`'ı hosted service olarak başlatma.
- Yeni Dockerfile stage'i ve compose servis tanımı; connector, compose
  ağında tam bağımsız bir container (`network_mode` paylaşımı YOK — bu
  yaklaşım denenip gerçek testte reddedildi, yukarıya bakın).
  `cloudflared`'ın tünel hedefi `http://api:5080` (compose servis DNS).
- `RelayConnectorSupervisor`'ın in-process hosted-service kaydını
  `RelaySettingsEndpoints.cs`'ten kaldırmak (supervisor artık `api` içinde
  hiç çalışmamalı — çifte çalışma riski yok) ve `/status` uç noktasının
  Postgres-tabanlı yeni reporter'ı kullanmasını sağlamak.
- Connector container'ının bağımsız durdurulup/yeniden başlatılabildiğinin,
  `api`'nin `/health/ready`'sinin bundan etkilenmediğinin (ve tersinin)
  kanıtı.

## Out of scope

- V0-ARC-009'un güven sınırlarını, protokollerini veya kimlik doğrulamasını
  değiştirmek (yalnız "Connector → POS" satırının mekanizması — yukarıdaki
  "bulunan sorun" nedeniyle, kullanıcı talimatıyla — değişti; owner/auth/
  public-port-yok kuralı aynı kaldı).
- `cloudflared` binary sürümü/pinning'i veya relay sağlayıcısının kendisi
  (V12-QRT-002).
- Başka herhangi bir modülün container sınırı (Kitchen printer dispatch
  vb.) — istenirse ayrı bir görev.
- Çoklu-şube/tenant ölçeklendirme.

## Dependencies

- V12-QRT-001
- V0-ARC-009

## Deliverables

- Yeni connector giriş noktası, Dockerfile stage'i ve compose servis tanımı.
- Yeni giriş noktası için task-specific otomatik testler (start/stop,
  token değişince cutover, crash-loop backoff — mevcut
  `RelayConnectorSupervisor` unit testleri büyük ölçüde değişmeden
  taşınabilir olmalı, çünkü kendi iç mantığı değişmiyor, yalnız onu
  barındıran şey değişiyor).
- Aşağıdaki acceptance evidence'ı kanıtlayan gerçek bir Docker Compose
  transcript'i (simüle edilmemiş).

## Acceptance evidence

Hepsi gerçek `docker compose` ile (ayrı, izole `-p alkaros-verify` projesi
altında, kullanıcının gerçek volume'una dokunmadan) doğrulandı — simüle
edilmemiş:

- `docker compose up --build --wait`, `api`/`web`/`postgres`'in yanında ayrı
  bir connector container'ı ayağa kaldırdı; `docker ps` bunu kendi
  container'ı olarak gösterdi. ✅
- Migration 125 (`relay_connector_status`) tam zincirde (006'dan 125'e
  kadar, 124 pozisyon) temiz uygulandı. ✅
- Yalnız connector container'ını yeniden başlatmak (`docker compose restart
  connector`), `api`'nin uptime'ını/healthy durumunu hiç etkilemedi. ✅
- Yalnız `api`'yi yeniden başlatmak connector container'ını ETKİLEMEDİ —
  `docker inspect .State.StartedAt` connector'ın başlangıç zamanının api'nin
  restart'ından ÖNCE olduğunu, log'lardaki "Application started" sayısının
  (2 — yalnız ilk `up` + benim elle yaptığım `restart connector`) api'nin
  restart'ından sonra ARTMADIĞINI kanıtladı. (İlk `network_mode: "service:api"`
  denemesinde bunun DOĞRU OLMADIĞI bulunmuş ve düzeltilmişti — yukarıya
  bakın.) ✅
- Connector, API'ye compose ağının servis DNS'i (`http://api:5080`)
  üzerinden, dar kapsamlı `relay-internal` ağı üzerinden erişir —
  `docker inspect`'in ayrı `Networks`/IP gösterimi + `wget http://api:5080/health/ready`'nin
  gerçek bir `200 {"status":"Ready"}` döndürmesiyle kanıtlandı. ✅
- NFC-relay-istisnası (ikinci bulunan sorun) düzeltmesi: aynı istek, api
  restart'ından SONRA da hâlâ `200` dönüyor — güvenlik geçidi hem doğru
  reddediyor (bridge-network genel IP aralığı DEĞİL) hem doğru kabul ediyor
  (yalnız `relay-internal`'deki connector). ✅
- `dotnet build ALKAROS.slnx -c Debug` → 0 Uyarı, 0 Hata.
- Yeni testler (DualScreenOptionsTests: 3, RelayConnectorStatusPublisherTests: 9,
  PostgresRelayConnectorStatusReporterTests: PublicGateway'in 21 testinin
  parçası) + regresyon (Manifest+DualScreenOptions birlikte 42/42,
  PublicGateway 21/21, LocalConnector 9/9) hepsi geçti.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.

## Handoff

- None

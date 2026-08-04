# ALKAROS başlangıç öncesi tamamlama planı

## Amaç

Bu belge, ilk yeni ürün görevi `V1-CAT-001` başlamadan önce kapatılacak gerçek
eksikleri, hataları, sıralamayı ve kabul kanıtını kilitler. Diskte kod bulunması
`Done` değildir; yalnız build, test, kanıt, dependency ve gate ile doğrulanan iş
`Done` olur.

## Doğrulanmış başlangıç durumu

- 260 görev: 18 `Done`, 67 `Blocked`, 175 `Planned`.
- V0: 18 `Done`, 47 `Blocked`.
- `V1-FND-001` ile `V1-FND-012` görevleri `Blocked` durumunda.
- 21 production projesinde hiç C# production dosyası yok; bunlar uygulanmış
  ürün özelliği değildir.
- Mevcut Git ağacındaki foundation kodu candidate evidence'dır.

## Değişmez çalışma kuralları

1. Her oturum yalnız bir `Task ID` yürütür.
2. Yazmadan önce task, dependency, gate, `Owned surface` ve exact allowlist okunur.
3. Kod yalnız allowlist içindeki üretim/test yollarına yazılır.
4. Stub, TODO, placeholder, sahte başarı, varsayımsal API/fallback, boş catch ve
   ölü kod yasaktır.
5. `Done`: allowlist diff, build, static analysis, ilgili testler, gerçek evidence
   ve taze bağımsız denetim gerektirir.
6. Private sözleşme, sandbox, cihaz veya mali/hukuki onay yoksa task `Blocked`
   kalır; davranış uydurulmaz.

## Aşama 0 — doğrulama ortamı

### Aşama 0.1 — execution-ready kayıt uzlaştırması

- `V0-GOV-029`, yeni candidate-code commitleri sonrasında ilk yürütülecek
  governance görevidir. `HostServiceRegistrationTests.cs` yalnız
  `V0-GOV-015` tarafından sahiplenilir; test hem Host composition hem module
  registry kullansa da Host DI composition sınırını doğrular.
- `AUDIT_REPORT.md` ve `AUDIT_MANIFEST.json` yeni Markdown ağacından yeniden
  üretilmeden, plan doğrulaması sıfır hataya inmeden ve coverage/manifest
  kontrolleri geçmeden hiçbir task kapanış kanıtı güncel sayılmaz.
- `ENV-*` klasörleri task kimliği değildir. Bunlar yalnız doğrulama ortamı
  kayıtlarıdır; bağımsız bir görevi `Done` yapmak, gate kapatmak veya ürün
  davranışını kanıtlamak için kullanılamaz.

### ENV-001 — .NET SDK onarımı

- Sorun: kurulu .NET 10.0.302 SDK'da
  `Microsoft.NET.Sdk.DefaultItems.Shared.targets` eksikti; onarım sonrası ikinci
  eksik dosya (`Microsoft.NET.ComposeStore.targets`) ortaya çıktı — kurulum
  bütünüyle bozuktu.
- Aynı sürümün winget ile force reinstall işlemi başarı mesajı verse de dosyayı
  getirmedi.
- Kapanış (2026-08-03): resmi SDK arşivinden
  `dotnet-sdk-10.0.302-win-x64.zip` indirildi, bozuk `10.0.302` klasörü
  `10.0.302.broken` yedeğine alındı, resmi `sdk/` ağacının tamamı
  `C:\Program Files\dotnet\sdk\10.0.302` konumuna açıldı.
- Kabul: hedef dosya mevcut; `dotnet --info`; aşağıdaki komut exit 0:
  `dotnet build ALKAROS.slnx --no-restore --warnaserror` (0 hata, 0 uyarı,
  kanıt: `evidence/ENV-001/**`).

### ENV-002 — PostgreSQL 18 test erişimi

- İlk tespit yanıltıcıydı: Windows `postgresql-x64-18` servisi `Running`
  görünse de gerçek durumda `postgres.exe` yoktu; loglar her bağlantıda
  `invalid value for parameter "timezone_abbreviations": "Default"` (PG 18'de
  kaldırılan parametre) FATAL'i veriyordu ve servis fiilen durmuştu.
- 5432 portunu Windows PostgreSQL değil, Docker (`lojinext-db-1`, postgres:16)
  dinliyordu. Çalışan PostgreSQL 18 ise `alkaros_test` container'ıydı
  (postgres:18.4, `0.0.0.0:5433->5432`).
- Kapanış (2026-08-03): test erişimi bu container üzerinden sağlandı;
  `ALKAROS_TEST_PG_PASSWORD` (container ortamından alınan parola) ve
  `ALKAROS_TEST_PG_PORT=5433` kullanıcı ortamına kaydedildi. Windows servisinde
  geçici olarak yazılan trust pg_hba.conf değişikliği orijinal SCRAM dosyasına
  geri alındı.
- Parola yalnız oturum ortamında `ALKAROS_TEST_PG_PASSWORD` ile verilir. Repo,
  kanıt dosyası veya command output'a secret yazılmaz.
- Kabul: kimlik doğrulamalı `SELECT 1` (port 5433); testler benzersiz test
  veritabanı oluşturur ve temizler (kanıt: `evidence/ENV-002/**`).

### ENV-003 — test matrisi

- 2026-08-03 tarihli önceki ENV-003 kaydı Architecture (5), Idempotency (60),
  Transactions (25), SensitiveData (23), Secrets (21),
  TransactionOutboxIntegration (11), Host (55) ve Identity.Authentication
  (34) sayımlarını taşır. Bu kayıt tarihsel candidate evidence'dır; sonraki
  commitlerde test sayıları değiştiği için güncel kabul sayımı değildir.
- Bu uzlaştırma sırasında yeniden çalıştırılan güncel sonuçlar: Architecture
  5/5, Host 60/60 ve Idempotency 71/71. Kalan paketler, ilgili task `Done`
  yapılmadan hemen önce aynı commit üzerinde yeniden çalıştırılır.
- Çözüm build'i warning/error olmadan geçer.
- `tests/Host/MigrationComposition/Program/ProgramArgumentTests.cs` (V1-SEC-003
  yüzeyi) 2026-08-03'te düzeltildi: eski senaryo down script içermeyen fixture'la
  `MissingDown` fail-closed validation'ının önüne geçilemeyecek bir mesaj
  bekliyordu; yeni senaryo up+down set ve manifest'te olmayan pozisyonla
  `--rollback`'in forward path'e düşmediğini kanıtlar.
- SDK/PG sürümü, komut ve exit code `evidence/ENV-003/**` altında kaydedilir.

## Aşama 1 — V0 karar ve dış kanıt kapısı

`GATE-V0-EXIT`, aşağıdaki açık görevler gerçek kanıtla `Done` veya tarihli/onaylı
`NotApplicable` olmadan kapanmaz.

### Mimari

- `V0-ARC-001` module dependency rules
- `V0-ARC-002` local-first sync contract
- `V0-ARC-003` idempotency/inbox/outbox contract
- `V0-ARC-004` API contract standard
- `V0-ARC-005` settings/secret classification
- `V0-ARC-006` notification delivery matrix
- `V0-ARC-007` deployment compatibility matrix
- `V0-ARC-008` release evidence contract
- `V0-ARC-009` QR relay topology

### Veri ve domain

- `V0-DAT-001`…`V0-DAT-006`: migration graph, value catalog, nullable/unique,
  projection ownership, key strategy ve migration rehearsal.
- `V0-DOM-001`…`V0-DOM-010`: lifecycle, bill/order, refund, allocation,
  reservation, discount, credit/invoice, reporting, receipt variance ve cost basis.
- `V0-DAT-002` ile `V0-CMP-002`, `V1-CAT-001`in doğrudan dependency'sidir;
  enum, money, tax veya business-date davranışı bunlar kapanmadan yazılmaz.

### Mali, güvenlik ve recovery

- `V0-CMP-001`…`V0-CMP-004`: GİB/e-Adisyon, money/tax/date, KVKK, fee/tip.
- `V0-SEC-001`, `V0-BKP-001`, `V0-BKP-002`, `V0-LIC-001`, `V0-DOC-001`.
- Vergi, mali, KVKK ve lisans kararları yetkili mali müşavir/hukuk onayı olmadan
  `Done` olmaz.

### Dış entegrasyonlar

- `V0-HUG-001`, `V0-QNB-001`, `V0-YSP-001`, `V0-MCD-001`, `V0-PRN-001`,
  `V0-QRG-001`.
- Kamuya açık doküman yalnız kamuya açık davranışı kanıtlar. İptal, webhook,
  sandbox, cihaz protokolü ve provider özel davranışı için karşı taraf kanıtı gerekir.

### Governance

- `V0-GOV-010`…`V0-GOV-016`: scope normalization, audit/manifest, sensitive
  envelope, retry backoff, atomic migration history ve post-remediation audit.
- Migration history/checksum/re-run/rollback precondition gerçek PostgreSQL'de
  test edilir.

## Aşama 2 — mevcut foundation kusurlarını kapat

| Öncelik | Sahip | Hata | Kapanış |
| --- | --- | --- | --- |
| 1 | `V1-FND-001`, `V1-FND-004` | Module registrations compose sonunda kayboluyordu. `9871193` descriptor/instance saklıyor; Host henüz bunları DI adapter'a uygulamıyor. | Host concrete registration'ı uygular; architecture testi geçer. |
| 2 | `V1-SEC-003` | `--rollback` parse edilip options'a aktarılmıyordu. `9a988a9` düzeltmeyi içerir; `ProgramArgumentTests` senaryosu down script eksikliği yüzünden fail-closed validation'la çelişiyordu (2026-08-03 düzeltildi). | Program ve Host migration testleri forward path'e düşmediğini kanıtlar. |
| 3 | `V1-FND-002` | Expired idempotency kaydı aktif replay/conflict gibi kalabiliyor; replay TTL yeniliyor; sweep production akışında yok. | Expired key atomik yeni kayıt; active same-hash replay TTL korur; different-hash conflict; concurrency testi. |
| 4 | `V1-FND-002`, `V0-GOV-015` | `CREATE TABLE IF NOT EXISTS`, uyumsuz şemayı kabul edip history'ye başarı yazabilir. | Schema doğrulaması fail-closed; uyumsuz şemada history yazılmaz. |
| 5 | `V1-FND-005` | Database dışı resource DB commit'ten önce commit olabiliyor; tam atomiklik iddiası yanlış. | External side-effect outbox/post-commit contract'a taşınır; failure testleri geçer. |
| 6 | `V1-FND-002`, `V1-FND-006` | Inbox/Outbox handler DB lock/transaction açıkken çağrılıyor. | Claim/lease transaction içinde, handler dışında; paralel worker testleri geçer. |
| 7 | `V1-IAM-001` | Expired lock resetlenmiyor; unknown-user timing farkı ve sınırsız hash iteration riski var. | Reset, constant-work ve bounded-iteration testleri geçer. |

## Aşama 3 — foundation kabul sırası

V0 kapandıktan sonra mevcut kod yeniden yazılmaz; acceptance evidence üretir ve
kusur varsa Aşama 2 altında düzeltilir.

1. `V1-FND-001`
2. `V1-FND-010`
3. `V1-FND-003`
4. `V1-FND-004`
5. `V1-FND-005`
6. `V1-SEC-001`
7. `V1-SEC-002`
8. `V1-FND-002`
9. `V1-FND-006`
10. `V1-FND-007`, `V1-FND-008`, `V1-FND-009`, `V1-FND-011`, `V1-FND-012`

Son beş görev audit/history/fixture/atomiklik/runtime manifest kanıtını günceller.
Eski evidence silinmez; güncel sonuçla çelişirse tarihsel candidate evidence olarak
etiketlenir.

## Aşama 4 — ilk yeni ürün kodu: V1-CAT-001

`V1-CAT-001` yalnız şu koşullarda `InProgress` olur:

- `GATE-V0-EXIT` kapalıdır.
- Aşama 3 zorunlu chain'i `Done`dur.
- `V0-DAT-002` ve `V0-CMP-002` `Done`dur.
- Tek gerçek assignee atanmıştır.
- Yalnız `src/Modules/Catalog/ProductCatalog/**`, ilgili test yüzeyi ve
  `database/migrations/V1/V1-CAT-001/**` yazılır.
- ENV-001…003 kanıtı günceldir.

Bu görevde yalnız Category, TaxProfile, Product, ModifierGroup ve Modifier domain
yönetimi yazılır. Geçerli tarihli fiyat, günlük menü ve UI kapsam dışıdır.

## Aşama 5 — V1-CAT-001 sonrası sıra

1. `V1-IAM-002` role/permission enforcement
2. `V1-OPS-001` append-only audit foundation
3. `V1-ORD-001` channel-independent Order aggregate
4. `V1-KIT-001` KitchenTicket lifecycle
5. `GATE-V1-EXIT`
6. `V11-UNT-001` dimension-safe unit/conversion
7. `V11-INV-004` stock master/location
8. `V11-INV-001` immutable stock ledger
9. `V11-INV-002` rebuildable stock balance projection

UI görevleri, bağlı domain/projection görevleri kapanmadan başlatılmaz.

## Her görev kapanış kontrolü

1. Başlangıç write-set snapshot kaydedilir.
2. Allowlist dışı yol olmadığı doğrulanır.
3. Build, static analysis, unit/integration ve migration testleri exit 0 verir.
4. Provider task'ında gerçek sandbox/device transcript vardır.
5. SHA-256, komut, exit code ve sonuç task evidence dizinine yazılır.
6. Taze bağımsız denetim geçer.
7. Ancak sonra `Done`; aksi durumda kaldırılma koşuluyla `Blocked`.

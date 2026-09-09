# V1-RMD-140 - QR/NFC public relay origin scope hardening

- Task ID: V1-RMD-140
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

QR'ın kendi müşteri sayfasını (`src/Apps/CustomerWeb`) inşa etmeye
başlamadan önce (Semih ile görüşülüp sıralama netleştirildi: relay kapsamı
önce), gerçek bir mimari sızıntı bulundu: `RelayProvisioningService`'in
Cloudflare Tunnel'i, provision edildiğinde **uygulamanın tamamını**
(`http://localhost:5080` — Cashier API'leri, yönetici panelleri dahil)
internete açıyordu, yalnızca NFC/QR müşteri yollarını değil.
`cloudflared`, `web`/Caddy'nin (yerel ağ için zaten kurulu olan B-4 origin-
ayırma mekanizması) hiç görmeden, aynı konteynerin içinden doğrudan
`localhost:5080`'e bağlanıyor. Bu, V1-RMD-139'un yalnızca **yerel** origin
paylaşımını (NFC ile Cashier aynı origin) kapattığı bulgudan bağımsız,
ondan daha büyük bir sızıntı: yerel değil, **kamuya açık**.

## Owned surface

- `plan/v1/remediation/V1-RMD-140-qr-relay-origin-scope-hardening.md`
  (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Host/DualScreen/DualScreenOptions.cs, DualScreenApplication.cs
    (V0-ARC-009/V1-RMD-139 sahipliğinde) — `--nfc-loopback-origin` yeni
    CLI bayrağı ve `NfcLoopbackOriginTrusted` seçeneği; V1-RMD-139'un NFC
    origin-gate middleware'i, üçüncü bir sinyal olarak
    `context.Connection.RemoteIpAddress`'in loopback olup olmadığını da
    kontrol edecek şekilde genişletildi. Port/header tabanlı iki mevcut
    sinyale dokunulmadı.
  - tests/Host/MigrationComposition/DualScreen/DualScreenOptionsTests.cs,
    tests/Host/Experience/Composition/ProductionExperienceCompositionTests.cs
    (aynı sahiplikler) — yeni doğrulama ve gerçek-HTTP kompozisyon testi.
  - compose.yaml, deploy/docker/Dockerfile (paylaşılan dağıtım
    konfigürasyonu) — `api` servisinin CMD/ENTRYPOINT'ine
    `--nfc-loopback-origin` eklendi; başka hiçbir bayrak/ayar değişmedi.
  - deploy/docker/README.md (paylaşılan dağıtım dokümantasyonu) — mevcut
    "Customer-display origin isolation (finding B-4)" bölümünün hemen
    altına, aynı üslupla yeni bir "QR/NFC public relay origin scope"
    bölümü eklendi.

## In scope

1. **Kök neden teşhisi.** `RelayProvisioningService.LocalOriginService`
   (`"http://localhost:5080"`) ve `compose.yaml`'ın `api` servisinin hiç
   port yayınlamadığı (`ports:` girdisi yok) doğrulandı: tünelin ulaştığı
   tek yol, `api` konteynerinin İÇİNDEN, loopback üzerinden — Caddy ise
   HER ZAMAN farklı bir konteynerden, Docker ağı üzerinden geliyor
   (asla loopback görünmüyor). Bu, sahte olarak taklit edilemeyen,
   yapısal bir ayrım sinyali.
2. **`--nfc-loopback-origin`** (opt-in, `--api-only` gerektirir — aynı
   `--nfc-origin-header`'ın kendi kısıtlaması): açıldığında, loopback
   kaynaklı bir istek, V1-RMD-139'un NFC origin-gate'i tarafından port/
   header sinyalleriyle birebir aynı şekilde ele alınıyor — yalnızca
   `/api/v1/nfc/*` sunuluyor, geri kalan her `/api/*` 404 dönüyor.
   Hiç yapılandırılmadıkça (varsayılan) hiçbir davranış değişmiyor.
3. **Gerçek dağıtım konfigürasyonuna bağlandı** — `compose.yaml`'ın `api`
   servisi ve `Dockerfile`'ın bağımsız `docker run` ENTRYPOINT'i artık bu
   bayrağı geçiriyor; başka hiçbir değişiklik gerekmiyor (Cloudflare API
   çağrılarının kendisi, `RelayProvisioningService`, hiç değişmedi).

## Out of scope

- QR'ın kendi HTTP yüzeyi/müşteri sayfası henüz yok, bu yüzden bugün bu
  gate yalnız NFC'yi koruyor — QR'ın uç noktaları yapıldığında (`/api/v1/qr/*`
  gibi) aynı `isNfcApi` kontrolüne eklenmesi gerekecek (tek satırlık bir
  genişletme, ayrı bir görev/adım).
- Gerçek bir Cloudflare hesabı/tüneliyle uçtan uca doğrulama — bu ortamda
  gerçek bir Cloudflare kaynağı yok; kanıt, konteyner-topolojisi
  muhakemesi (port yayınlanmıyor, `cloudflared` aynı konteynerde) ve
  gerçek HTTP çağrılarıyla test edilen middleware mantığına dayanıyor,
  gerçek bir tünelin bu yeni bayrağı gerçekten kullandığının fiziksel
  doğrulamasına değil.
- `src/Apps/CustomerWeb` — ayrı, sıradaki görev.

## Dependencies

- V1-RMD-139
- V12-QRT-001

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- Gerçek Postgresql'e karşı Docker'da, real exit code 0 ile:
  - `ALKAROS.Host.Experience.Composition.Tests`: **7/7** (yeni
    `LoopbackOriginTrustedRestrictsALoopbackCallerToTheNfcRouteAllowlist`
    dahil — gerçek bir loopback HTTP çağrısının Cashier API'sinden
    reddedilip NFC API'sine ulaştığı doğrulandı).
- `dotnet test tests/Host/MigrationComposition` (`DualScreenOptionsTests`,
  yerel, Postgres gerekmez): **15/15** (1 yeni — `--nfc-loopback-origin`
  `--api-only` gerektiriyor).
- `python -m pytest tests/Architecture/ProjectManifest/test_project_manifest.py`:
  4/4.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var
  olan, ilgisiz ihlal (değişmedi), yeni ihlal yok.

## Handoff

- None

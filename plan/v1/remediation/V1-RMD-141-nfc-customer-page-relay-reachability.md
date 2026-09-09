# V1-RMD-141 - NFC customer page relay reachability

- Task ID: V1-RMD-141
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

Semih'in QR/NFC eşdeğerliği hakkındaki sorusuna cevaben bulunan gerçek bir
boşluk: V1-RMD-140, `--nfc-loopback-origin` ile NFC/QR API'lerini röle
üzerinden erişilebilir kıldı, ama NFC'nin kendi müşteri **sayfası**
(`/nfc/{tableId}`) hâlâ yalnızca PosTerminal paketinin bir parçası olarak
`web` (Caddy) tarafından sunuluyordu — Cloudflare Tunnel bağlayıcısının
loopback bağlantısı `web`'e hiç ulaşmıyor. Sonuç: NFC etiketinin URL'i tünel
host adını gösterirse orada sayfayı sunacak hiçbir şey yoktu; LAN adresini
gösterirse sayfa açılıyordu ama kendi API çağrıları origin-gate tarafından
reddediliyordu (Caddy `/api/v1/nfc/*`'i nfc-origin olarak etiketlemiyor).
QR için V12-CWB-001'de kurulan `--qr-web-root` deseninin simetriği NFC için
de kuruldu.

## Owned surface

- `src/Clients/PosTerminal/nfc.html`, `src/Clients/PosTerminal/src/nfc-entry.tsx`,
  `src/Clients/PosTerminal/vite.nfc.config.ts` (yeni) — `NfcOrder.tsx`'i
  doğrudan mount eden, `App.tsx`'in route dispatch'ini hiç içermeyen ayrı bir
  build girişi; bu sayede Cashier/RelaySettings/ReservationStation'ın kodu
  röle origin'ine hiç girmiyor (V1-RMD-139'un API katmanındaki izolasyon
  hedefinin statik pakette de karşılığı).
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - src/Host/DualScreen/DualScreenOptions.cs, DualScreenApplication.cs
    (V0-ARC-009/V1-RMD-139/V1-RMD-140/V12-CWB-001 sahipliğinde) —
    `--nfc-web-root <path>` yeni CLI bayrağı ve `NfcWebRoot` seçeneği;
    `--qr-web-root`'un simetriği, ama `/nfc/{tableId}`'nin değişken segmenti
    hiçbir literal dosyayla eşleşmediği için `UseStaticFiles`'ın yanına,
    `/nfc` altındaki eşleşmeyen her GET için `index.html`'i doğrudan sunan
    ayrı bir SPA-fallback middleware'i eklendi.
  - tests/Host/MigrationComposition/DualScreen/DualScreenOptionsTests.cs,
    tests/Host/Experience/Composition/ProductionExperienceCompositionTests.cs
    (aynı sahiplikler) — `--nfc-web-root` doğrulama testleri, gerçek statik
    dosya sunumu + değişken tableId segmenti için SPA-fallback testi.
  - src/Clients/PosTerminal/package.json (V0-CMP-005/PosTerminal client
    sahipliğinde) — yeni `build:nfc-standalone` script'i eklendi, mevcut
    `build`/`typecheck`/`test` script'lerine dokunulmadı.
  - src/Clients/PosTerminal/.gitignore (aynı sahiplik) — yeni `dist-nfc/`
    girdisi.
  - compose.yaml, deploy/docker/Dockerfile (paylaşılan dağıtım
    konfigürasyonu) — `frontend-build` aşamasına `pnpm build:nfc-standalone`
    adımı, `api` imajına `--nfc-web-root /app/nfc-web` ve ilgili `COPY`
    eklendi; `web`/Caddy tarafına hiçbir şey eklenmedi (bkz. In scope).
  - deploy/docker/README.md (paylaşılan dağıtım dokümantasyonu) — "QR/NFC
    public relay origin scope" bölümüne NFC'nin statik sayfası hakkında not
    eklendi.

## In scope

- NFC'nin müşteri sayfasının, QR'ın kendi küçük paketine simetrik olarak,
  `api` konteynerinden doğrudan (loopback/tünel üzerinden) sunulması.
- Yalnızca **röle yolunun** simetrisi — `web`/Caddy'nin LAN yoluna
  (`--nfc-origin-header` + özel bir vhost) kasıtlı olarak dokunulmadı: bu,
  QR sorusuna cevaben Semih'e ayrıca bildirildi, henüz onay alınmadı, ayrı
  bir görev/karar.

## Out of scope

- `web`/Caddy'nin LAN yolu üzerinden NFC/QR API erişimi (bugün her ikisi de
  orada 404 dönüyor — bu görevden önce de böyleydi, bu görev tarafından ne
  düzeltildi ne de kötüleştirildi). Semih'e ayrıca bildirildi.
- QR'ın kendi statik sayfasının Caddy'ye eklenmesi — V12-CWB-001'in kendi
  gerekçesiyle (yarı-çalışır tuzak) kasıtlı olarak yapılmadı, bu görev de
  değiştirmedi.

## Dependencies

- V1-RMD-139
- V1-RMD-140
- V12-CWB-001

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- `pnpm build` (PosTerminal, ana paket): değişmedi, hâlâ tek `dist/index.html`
  girişi üretiyor, `NfcOrder-*.js` kendi ayrı lazy chunk'ı olarak kalıyor
  (4.84 kB, V12-NFC-003'ün kendi ölçümüyle aynı) — yeni `nfc.html` ana build'e
  hiç karışmıyor.
- `pnpm build:nfc-standalone` (yeni): `dist-nfc/index.html` + kendi
  `assets/nfc-*.js` (200.80 kB, React dahil) + `assets/nfc-*.css` üretiyor,
  tüm asset referansları `/nfc/...` ile başlıyor (`base: "/nfc/"`).
- `pnpm test` (PosTerminal, vitest): **136/136** (22 dosya) — regresyon yok.
- Gerçek Postgresql'e karşı Docker'da, real exit code 0 ile:
  - `ALKAROS.Host.Tests` (`DualScreenOptionsTests` alt kümesi): **21/21**
    (yeni 3 test: `--nfc-web-root` doğrulamaları).
  - `ALKAROS.Host.Experience.Composition.Tests`: **10/10** (yeni: gerçek
    statik dosya sunumu + değişken tableId segmenti için SPA-fallback,
    /nfc dışındaki yolların etkilenmediği doğrulandı).
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var olan,
  ilgisiz ihlal (değişmedi), yeni ihlal yok.
- **Gerçek Docker Compose ile uçtan uca doğrulama** (`docker compose
  build/up`, gerçek `alkaros-postgres-1`/`alkaros-api-1` konteynerleri, aynı
  loopback röle topolojisi simülasyonu):
  - `docker exec alkaros-api-1 curl http://127.0.0.1:5080/nfc/{herhangi-bir-id}`:
    **200**, gerçek `index.html`; `/nfc/assets/nfc-*.js`: **200** —
    NFC'nin sayfası, gerçek röle topolojisinin (yalnızca loopback, hiç
    yayınlanmış port yok) simülasyonuyla ilk kez uçtan uca yüklendi.
  - Elle oluşturulmuş bir masa + ürün ile: `GET /api/v1/nfc/tables/{id}/catalog`
    → gerçek ürün listesi; `POST /api/v1/nfc/tables/{id}/orders` → **200**,
    `status: "Accepted"` (güvenilir kanal, doğrudan kabul) — NFC'nin TÜM
    akışı (sayfa + menü + sipariş) aynı loopback yolu üzerinden gerçek
    şekilde doğrulandı.
  - `curl http://127.0.0.1:5080/api/v1/terminals/.../catalog` (Cashier API):
    **404** — origin-gate'in ana koruması regresyona uğramadı.
  - Test verisi (masa, bölge, ürün, sipariş, kitchen ticket) doğrulama
    sonrası veritabanından silindi.

## Handoff

- None

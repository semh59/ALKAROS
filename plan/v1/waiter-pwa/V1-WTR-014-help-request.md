# V1-WTR-014 - "Yardım çağır" sinyali

- Task ID: V1-WTR-014
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`garson-karsilastirma` karşılaştırma dokümanının "Yeni fikirler" bölümünde
(Katman A, madde 3) önerilen üçüncü özellik. Garson→yönetici gerçek zamanlı
hiçbir kanal yoktu — masaya gidip anlatmak dışında. Semih'in onayladığı
parametreler: yalnız yönetici/amir (kasiyer hariç), kısa bir tip seçimi
(döküldü/şikayet/onay/diğer), aynı masa için 2 dakika soğuma süresi.

Semih'in ikinci kararı (netleştirici soru üzerine, 2026-09-11): alıcı
PosTerminal'i (React) kullanıyor, orada bugüne kadar hiçbir gerçek-zamanlı
bildirim mekanizması yoktu — bu görev bilinçli olarak PosTerminal'e yeni bir
SignalR bağlantısı + bildirim banner'ı ekleyerek tam kapsamlı yapıldı,
sunucu-taraf-yalnız bir yarım çözüm olarak bırakılmadı.

Mimarî: `WaiterOrderStatusHub`'ın (garson bildirimleri) aynısı ama TERSİ
yönde ve FARKLI bir oturum modeliyle — `HelpRequestHub` yönetim çerezini
(`alkaros.manager`, `ManagementSessionLookup`) kullanıyor, kasiyer çerezini
değil, çünkü alıcı kitle kasiyer değil yönetici/amir. Kendi Experience
alanı (`Experience/HelpRequests`), Orders'a katılmadı — `WaiterNotifications`
neden Orders'a katılmadıysa aynı sebep: alıcı tarafı tamamen farklı bir
çerez/oturum modeliyle kimlik doğruluyor.

2 dakikalık soğuma, bellek içi değil `notifications.help_requests`'te
tutuluyor — bir restart'ta sıfırlanmasın, birden fazla Host örneği arasında
paylaşılsın diye (`notifications.serving_handoff_notes`'un V1-WTR-013'te
aldığı aynı karar). Aynı tablo, kimin ne zaman ne çağırdığının gerçek bir
denetim izini de veriyor.

PosTerminal tarafında (`Cashier.tsx`) bağlantı her oturum için deneniyor,
istemci tarafında role göre filtrelenmiyor — kimin gerçekten alacağına
`HelpRequestHub`'ın kendi çerez kontrolü karar veriyor (bir kasiyer
oturumunun bağlantısı sunucuda sessizce reddediliyor, hiçbir maliyeti yok).

## Owned surface

- `plan/v1/waiter-pwa/V1-WTR-014-help-request.md` (yeni)
- `database/migrations/V1/V1-WTR-014/**` (yeni)
- `src/Host/Experience/HelpRequests/**` (yeni)
- `tests/Host/Experience/HelpRequests/**` (yeni)
- `src/Clients/PosTerminal/src/routes/Cashier.help-alerts.test.tsx` (yeni)
- Sınırlı ek:
  - src/Host/DualScreen/DualScreenApplication.cs (DualScreen sahipliğinde)
    — `AddHelpRequestExperience()`/`MapHelpRequestApi()` kaydı.
  - database/MigrationComposition/order.json — 101 kaydı.
  - src/Host/Composition/Migrations/MigrationManifest.cs — PhaseBMax.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs — manifest
    testinin sınır değerleri.
  - tools/consistency-audit/consistency_audit.py — `HOST_AREA_SCHEMA`'ya
    yeni alanın şema sahipliği eklendi (`Experience/HelpRequests` →
    `notifications`).
  - docs/CONSISTENCY_AUDIT.md — yukarıdaki eşlemenin özet satırı.
  - ALKAROS.slnx — yeni test projesinin çözüme eklenmesi.
  - src/Clients/WaiterPwa/wwwroot/index.html, waiter-app.js (V1-WTR-010
    sahipliğinde) — adisyon başlığına "Yardım çağır" düğmesi, tip seçim
    sheet'i.
  - src/Clients/PosTerminal/src/routes/Cashier.tsx, styles.css
    (PosTerminal sahipliğinde) — SignalR bağlantısı, bildirim banner'ı.

## Out of scope

Diğer "Katman A" fikri (taslakta fiyat/stok değişti işareti) — ayrı görev.
Uygulama kapalıyken de ulaşan Web Push kanalı (yönetici tarafı için henüz
abonelik akışı yok) — V1-WTR-011'in kendisi de aynı şekilde önce SignalR'la
başlayıp push'u ayrı bir görevde eklemişti, aynı kademeli yol.

## Dependencies

- V1-RMD-121
- V1-WTR-009
- V1-WTR-011

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` (tüm çözüm) → 0 uyarı, 0 hata.
- `node --check waiter-app.js` → temiz.
- PosTerminal: `npm run typecheck` → temiz; `npm run test -- --run` →
  137/137 yeşil (136 mevcut + 1 yeni `Cashier.help-alerts.test.tsx`).
  Yeni testin vacuous olmadığı kanıtlandı: `Cashier.tsx`'teki
  `"HelpRequested"` olay adı geçici olarak `"HelpRequestedXXX"` yapıldı,
  test gerçekten kırmızıya döndü (`handlers.HelpRequested` tanımsız),
  sonra geri alındı, tekrar yeşile döndü.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test`:
  - `tests/Host/Experience/HelpRequests/*.csproj` → 6/6 yeşil (istek
    başarıyla kaydedilir; aynı masa 2 dakika içinde ikinci istek 429;
    farklı masa etkilenmez; geçersiz tip 400; olmayan masa 404; oturumsuz
    401).
  - `tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj`
    → 9/9 yeşil.
  - `tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj --filter
    "FullyQualifiedName~ManifestTests"` → 16/16 yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.
- `tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj --filter
  "FullyQualifiedName~DualScreenAuthorizationHttpTests"` → 5/5 yeşil. (Bu
  turda bir kez, gerçek bir sistem kaynağı tükenmesiyle karşılaşıldı —
  test Postgres container'ı `could not fork new process for connection:
  Resource temporarily unavailable` veriyordu, Postgres loglarında
  doğrulandı. `wsl --shutdown` + Docker Desktop'ın yeniden başlatılmasıyla
  düzeltildi, bu görevin kodundan bağımsız bir ortam sorunuydu — sonra bu
  test de dahil her şey yeniden koşuldu, hepsi yeşil.)

## Handoff

- None

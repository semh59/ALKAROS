# V1-RMD-157 - Kasa hızlı satışı: sahte masa hiç var olmuyordu, iki müşteri birleşiyordu

- Task ID: V1-RMD-157
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin Backend bölümünden iki
madde, aynı kökten: kasa hızlı satışı `tableId:
'00000000-0000-0000-0000-000000000001'` (`KASA-1`) sabit UUID'sini
kullanıyor (`src/Clients/Cashier/wwwroot/cashier-app.js:294-295`).

**Denetimin bulduğundan daha ciddisi, kodu izlerken çıktı:** bu UUID'ye
karşılık gelen bir `table_mgmt.tables` satırı **hiçbir yerde
oluşturulmuyor** — ne migration'larda, ne `deploy/docker/seed-demo.sql`'de
(o zaten "NOT for production" diyor), ne testlerde. Depoda bu UUID'nin
geçtiği tek yer istemci dosyasının kendisi. `orders.orders.table_id` bu
satıra foreign key taşıyor ve Orders'ın hata eşleyicisinde
`ForeignKeyViolation`'a özel bir dal yok (Catalog'un aksine) — yani gerçek
bir kurulumda **her kasa hızlı satışı** 503 "Veritabanı işlemi
tamamlanamadı" ile başarısız olurdu, kasiyere yanlış teşhis verip
çevrimdışı kuyruğun onu sonsuza kadar tekrar denemesine yol açardı.

**Denetimin bulduğu:** satır var olsa bile, V1-ORD-006'nın "masaya bağlı
kapanmamış hesap varsa yeni tur ona eklenir" kuralı KASA-1 için yanlış
davranıyor — orası gerçek bir masa değil, art arda gelen ilgisiz
müşterilerin ortak kullandığı sabit bir uç nokta. Müşteri 1'in
`table-draft`'ı başarılı olup `submit-draft`'ı başarısız olursa (503, kopan
bağlantı), Draft sipariş KASA-1'e bağlı kalır. Müşteri 2 kasaya gelip yeni
bir satış başlattığında, siparişi bu bağlı Draft'a **birleşir** — iki
ilgisiz müşterinin siparişi tek hesapta karışır.

## Owned surface

- `plan/v1/remediation/V1-RMD-157-cashier-quick-sale-table-provisioning.md` (yeni)
- `database/migrations/V1/V1-RMD-157/**` (yeni)
- Sınırlı ek:
  - database/MigrationComposition/order.json — 098 kaydı.
  - src/Host/Composition/Migrations/MigrationManifest.cs — PhaseBMax.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs — sınır
    değerleri.
  - src/Clients/Cashier/wwwroot/cashier-app.js (V1-CSH-00x sahipliğinde) —
    her gönderim denemesinden sonra masa serbest bırakılır.

## In scope

1. **KASA-1 satırı gerçekten var olsun.** Yeni migration, sabit UUID'yle
   `table_mgmt.tables`'a idempotent bir satır ekler (`zone_id NULL`,
   `active TRUE`, `current_status 'Available'`) — kasa hızlı satışı artık
   kurulum sonrası ilk denemede çalışır, "veritabanı ulaşılamaz" yalanı
   söylemez.
2. **Her deneme sonrası masa serbest bırakılır.** Kasa istemcisi, gönderim
   başarılı da olsa (siparişi kasaya gönder — hesap kendi kimliğini korur,
   masa anında boşalır) başarısız da olsa (Draft yarım kalmışsa da aynı
   çağrı masayı serbest bırakır, terk edilmiş Draft hesap açıkta kalmaz)
   `POST .../orders/{orderId}/send-to-cashier` çağırır. Böylece bir sonraki
   müşteri her zaman temiz bir masa bulur, hiçbir zaman öncekinin
   siparişine birleşmez.

## Out of scope

- Kasa hızlı satışının masa modelinden tamamen ayrılması (örn. `table_id`
  nullable yapmak): daha büyük bir mimari değişiklik, bu görevin kapsamı
  değil. Bugünkü çözüm KASA-1'i gerçek bir masa gibi ele almaya devam
  ediyor; Masa Yönetimi ekranında görünüp durum değiştirmesi kozmetik bir
  yan etkidir, ayrı bir karar.
- `send-to-cashier`'ın "ödeme bekleyen hesaplar" listesine düşürdüğü kasa
  hızlı satışlarının o listeden nasıl temizleneceği: ödeme V1'de yok
  (bilinen sınır, `docs/design/modules/check-and-table.md`).

## Dependencies

- V1-RMD-156
- V1-ORD-006

## Acceptance evidence

- `dotnet build tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj -c Debug` → 0 uyarı, 0 hata.
- Scratch veritabanı `rmd157_scratch` üzerinde 001'den 098'e kadar (97
  dosya) tüm `.up.sql` migration'ları sırayla, hatasız uygulandı.
- `SELECT ... FROM table_mgmt.tables WHERE table_id =
  '00000000-0000-0000-0000-000000000001'` → KASA-1 satırı gerçekten var:
  `zone_id NULL`, `active true`, `current_status 'Available'`.
- `098-cashier-quick-sale-table.down.sql` çalıştırıldı → satır silindi
  (`DELETE 1`, ardından count 0); `.up.sql` tekrar çalıştırıldı → satır
  geri geldi (`INSERT 0 1`, count 1). Down/up round-trip temiz.
- Denetimin tarif ettiği tam başarısızlık senaryosu tersine çevrilerek
  kanıtlandı: `orders.orders` tablosuna `table_id =
  '00000000-0000-0000-0000-000000000001'`, `source = 'Cashier'` ile gerçek
  bir satır eklendi ve başarıyla kabul edildi (`INSERT 0 1`) — migration
  öncesi bu insert `fk_orders_table` ihlaliyle reddedilirdi.
- `tests/Host/MigrationComposition/Manifest/ManifestTests.cs` — 098 girdisi
  için `RuntimeManifestIds`, `LastEntryTables`, migration sayısı (97) ve
  `ManifestRejectsPhaseBIdOutsideItsRange` probu (099) güncellendi; 16/16
  manifest testi yeşil.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet
  test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj -c Debug
  --no-build` → gerçek test Postgres'ine karşı tüm proje: **134/134
  yeşil**, `MigrationExecutionTests.AppliesAllMigrationsInManifestOrderOnEmptyDatabase`
  dahil (098 migration'ı boş bir veritabanında manifest sırasıyla gerçekten
  uygulanıyor).
- `src/Clients/Cashier/wwwroot/cashier-app.js` — `dispatchOrderToKitchen()`
  artık `table-draft` başarıyla döndükten sonra (submit başarılı da olsa,
  başarısız da olsa) `POST
  /api/v1/terminals/{terminalId}/orders/{orderId}/send-to-cashier` çağırıyor
  ve masayı serbest bırakıyor; serbest bırakma isteği başarısız olursa
  sadece `console.error`'a düşer, kasiyere gösterilen gerçek sonucu (mutfağa
  iletildi / hata) hiçbir zaman gölgelemez. `node --check` ile sözdizimi
  doğrulandı (proje içinde bu dosya için ayrı bir JS test paketi yok).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı
  (707 markdown, 685 task, 1741 bağımlılık kenarı).
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. (Kalan tek ihlal —
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`
  — bu görevden önce vardı, bu görevin sahip olduğu yüzeyin dışında;
  ayrıca not edildi, düzeltmesi ayrı bir göreve bırakıldı.)

## Handoff

- None

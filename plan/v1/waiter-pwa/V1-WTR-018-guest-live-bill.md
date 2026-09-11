# V1-WTR-018 - Misafir için salt-okunur canlı adisyon

- Task ID: V1-WTR-018
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Goal

`garson-karsilastirma` karşılaştırma dokümanının "Yeni fikirler" bölümünden
6. madde (Katman B): misafirin kendi telefonundan, QR oturumuyla, masasının
canlı adisyonunu (sipariş edilen kalemler + ara toplam/KDV/toplam) salt
okunur görmesi.

**Önce kapsamın gerçek büyüklüğü netleştirilmesi gerekti:** Semih'e soruldu
— repoda QR müşteri istemcisi (HTML/JS) hiç yoktu sandım, ama araştırınca
`src/Apps/CustomerWeb/{Menu,OrderEntry}/wwwroot` altında zaten gerçek,
çalışan bir QR misafir istemcisi (V12-CWB-001/002) olduğu ortaya çıktı —
yalnızca `src/Clients/` altında aramıştım, `src/Apps/CustomerWeb/` farklı
bir kök dizin. Bu, işi "sıfırdan yeni istemci" değil "var olan istemciye
üçüncü bir sayfa eklemek" haline getirdi — Menu (menü gezinme) ve
OrderEntry'nin (sepet/gönderim) yanına Bill (salt-okunur canlı adisyon).

**Sunucu tarafı:** `DualScreenStore.GetLiveBillAsync(tableId)` (yeni
`DualScreenStore.QrBill.cs` parçalı dosyası) — `table_mgmt.tables.
current_order_id`'yi okur, `orders.orders`/`orders.order_items`'ı
`GetSnapshotAsync`'in (fiziksel müşteri ekranı) kullandığı aynı ham-SQL
okuma-modeli yaklaşımıyla sorgular. Kapanmış/iptal edilmiş bir adisyon
(`Completed`/`Cancelled`/`Rejected`) "aktif sipariş yok" durumuna düşer —
misafir asla artık kendisine ait olmayan bir tutarı görmez.
`GET /api/v1/qr/bill`, `/menu` ile aynı oturum-doğrulama desenini kullanır
(session header → `CustomerSessionService.ValidateAsync` → `TableId`).

**İstemci tarafı:** `bill.html`/`bill.js`/`bill.css`, Menu ve OrderEntry
sayfalarının aynı sessionStorage sözleşmesini (`alkaros.qr.sessionToken`,
`alkaros.qr.tableToken`) ve aynı oturum-yeniden-deneme desenini
kullanıyor — bilinçli olarak ayrı bir modül değil, o iki sayfanın da
yaptığı gibi kopyalanmış. 5 saniyede bir `/api/v1/qr/bill`'i yokluyor;
başarısız bir yoklama bir sonrakini durdurmuyor (misafirin ağ kesintisi
sayfayı kalıcı olarak eski bir tutarda dondurmamalı). Menu ve OrderEntry'nin
başlıklarına "Adisyonum" bağlantısı eklendi.

## Owned surface

- `plan/v1/waiter-pwa/V1-WTR-018-guest-live-bill.md` (yeni)
- `src/Apps/CustomerWeb/Bill/wwwroot/**` (yeni)
- `tests/Apps/CustomerWeb/Bill/test_customer_web_bill.py` (yeni)
- `src/Host/DualScreen/DualScreenStore.QrBill.cs` (yeni) — `GetLiveBillAsync`.
- Sınırlı ek:
  - src/Host/DualScreen/DualScreenContracts.cs (Host sahipliğinde) —
    `QrLiveBillDto`, `QrLiveBillLineDto`.
  - src/Host/Experience/QrOrdering/QrOrderingEndpoints.cs (Host
    sahipliğinde) — `GET /api/v1/qr/bill`.
  - src/Apps/CustomerWeb/Menu/wwwroot/index.html, menu-app.css;
    src/Apps/CustomerWeb/OrderEntry/wwwroot/order-entry.html,
    order-entry.css (V12-CWB-001/002 sahipliğinde) — "Adisyonum" bağlantısı.
  - deploy/docker/Dockerfile (deploy sahipliğinde) — Bill'in wwwroot'unu
    `qr-web`'e kopyalayan satır.
  - tests/Host/Experience/QrOrdering/{QrOrderingHttpTests.cs,
    QrOrderingTestDatabase.cs} (QrOrdering test sahipliğinde) — yeni testler
    + `SeedActiveOrderAsync` yardımcı fonksiyonu.
  - tests/Host/Experience/QrOrdering/ALKAROS.Host.Experience.QrOrdering.Tests.csproj
    — migration 103 fixture eklemesi (V1-WTR-017'nin kendi blast-radius
    kaçırması, bu görevle aynı commit'te düzeltildi — bkz. V1-WTR-017'nin
    kendi task dosyası).

## Out of scope

- Gerçek zamanlı (SignalR/push) güncelleme — 5 saniyelik yoklama yeterli
  görüldü; bu, sunucunun WaiterPwa'ya zaten sunduğu itme karmaşıklığını
  (V1-WTR-014'ün "yardım çağır" kararının aksine) misafir tarafında
  gerektirmiyor, aynı `QrPendingOrderStore` yoklama deseniyle tutarlı.
- Ödeme veya hesap kapama — bu ekran yalnızca okuma; `/comp`,
  `/transfer-server` gibi diğer QR/garson yazma uçlarıyla aynı bilinçli
  sınır.

## Dependencies

- V12-CWB-001
- V12-CWB-002

## Acceptance evidence

- `dotnet build ALKAROS.slnx -c Debug` (tüm çözüm) → 0 uyarı, 0 hata.
- `node --check bill.js` (ve regresyon için menu-app.js, order-entry.js) →
  temiz.
- `python -m pytest tests/Apps/CustomerWeb -q` → 13/13 yeşil (Menu 4 +
  OrderEntry 4 + Bill 5).
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
  ALKAROS_KITCHEN_STATION_ID=test-station dotnet test`:
  - `tests/Host/Experience/QrOrdering` → 20/20 (3 yeni test: sipariş
    yokken adisyon boş/aktif-değil döner; sipariş varken canlı satırlar
    ve toplamlar doğru; geçersiz oturum 401).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.

## Handoff

- None

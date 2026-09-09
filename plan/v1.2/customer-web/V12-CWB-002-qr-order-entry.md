# V12-CWB-002 - Build QR order entry

- Task ID: V12-CWB-002
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:II.2.18
- PDF:II.6.8
- PDF:II.7.3
- PDF:III.21

## Goal

QR customer'ın açık final summary ile Order oluşturup PendingConfirmation workflow'una göndermesini sağlamak.

## Owned surface

- `src/Apps/CustomerWeb/OrderEntry/**`, `tests/Apps/CustomerWeb/OrderEntry/**` (yeni) — sepet düzenleme, not
  girişi, fiyat özeti, gönderim ve beklemede-order durumu poll'u.
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik
  iddiası olarak parse etmesin):
  - src/Host/Experience/QrOrdering/**, tests/Host/Experience/QrOrdering/**
    (V12-CWB-001 sahipliğinde) — aynı route grubuna `POST /api/v1/qr/orders`
    (sipariş gönderimi, `QrPendingOrderStore.SubmitAsync`'i olduğu gibi
    kullanıyor) ve `GET /api/v1/qr/orders/{submissionId}` (poll,
    `FindResultingOrderAsync`) eklendi; `AddQrOrderingExperience`'a
    `ITableRepository`/`QrTableReservationPolicy`/`QrPendingOrderStore`'un
    kendi kendine yeten (self-contained) zinciri eklendi; exception filter'a
    `QrTableNotFoundException`/`QrTableNotAvailableException`/
    `QrOrderInvalidProductException` eşlemesi eklendi. Test veritabanı
    fixture listesine orders/outbox migration'ları eklendi.
  - src/Apps/CustomerWeb/Menu/wwwroot/index.html, menu-app.js, menu-app.css,
    tests/Apps/CustomerWeb/Menu/test_customer_web_menu.py (V12-CWB-001
    sahipliğinde) — her ürün kartına "Sepete ekle" butonu, paylaşılan
    `alkaros.qr.cart` sessionStorage sözleşmesini yazan `CartStore`, ve
    OrderEntry'nin kendi sayfasına giden bir sepet çubuğu eklendi. Var olan
    "sepet asla yok" testi, gerçekliği yansıtacak şekilde güncellendi (sepet
    biriktirme kapsam içinde, yalnızca gönderim CWB-001'in kapsamı dışında
    kalmaya devam ediyor).
  - deploy/docker/Dockerfile (paylaşılan dağıtım konfigürasyonu) — `api`
    imajına `src/Apps/CustomerWeb/OrderEntry/wwwroot` da aynı `qr-web`
    dizinine kopyalandı (Menu'nün dosyalarıyla birleşiyor, ikisi de aynı
    `--qr-web-root`'tan sunuluyor).
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Sepet düzenleme (adet artır/azalt, kaldır), her satır için not girişi, fiyat özeti (satır toplamı + genel
  toplam), yinelenen gönderim koruması (kalıcı `submissionId`, sayfa yeniden yüklense bile aynı kimlikle
  tekrar dener — `QrPendingOrderStore.SubmitAsync`'in kendi idempotency'siyle eşleşiyor) ve beklemede-order
  durumu (asenkron materyalizasyonu poll ederek gösterme).
- `POST /api/v1/qr/orders` (gönderim) ve `GET /api/v1/qr/orders/{submissionId}` (poll) HTTP uç noktaları.

## Out of scope

- Doğrudan mutfağa gönderim (bu zaten yok — QR siparişleri her zaman `PendingConfirmation`'da bekler,
  docs/design/modules/qr-nfc-ordering.md), müşteri payment, personel onayı (`PendingOrderConfirmationStore`'un
  kendi Accept/Reject HTTP yüzeyi, V1-RMD-137, ayrı bir yönetici ekranı — bu sayfa hiç çağırmıyor) ve menü
  yönetimi.

## Dependencies

- V12-CWB-001
- V12-QRO-001
- V12-QRO-002
- V0-CMP-005

## Deliverables

- QR müşteri order giriş arayüzü (`src/Apps/CustomerWeb/OrderEntry/wwwroot`) ve API contract testleri.
- Yinelenen tıklama, son kullanma tarihi, öğe kullanılamıyor, fiyat değişikliği ve table durumu testleri.

## Acceptance evidence

- Submit yalnız tek PendingConfirmation QR Order üretir; approved confirmation öncesinde KitchenTicket oluşturamaz
  veya stok ayıramaz — `QrOrderSubmittedConsumer` (zaten Done, V12-QRO-001) Order'ı Draft → Submitted →
  PendingConfirmation'a taşıyor ve orada durduruyor, tıpkı NFC'nin yaş-kısıtlı siparişleri gibi; kitchen ticket
  yalnızca `SubmitOrderHandler`'ın kendi tek-seferlik dispatch'i üzerinden, staff onayından SONRA değil.
- `dotnet build ALKAROS.slnx -c Debug`: 0 Uyarı, 0 Hata.
- Gerçek Postgresql'e karşı Docker'da, real exit code 0 ile:
  - `ALKAROS.Host.Experience.QrOrdering.Tests`: **17/17** (yeni 6 test: gönderim + masa rezervasyonu, tekrar
    gönderim idempotency, boş sepet reddi, dolu masa reddi, beklemede poll, geçersiz oturumla poll reddi).
  - `ALKAROS.Host.Experience.NfcOrdering.Tests`: **17/17** (regresyon yok).
  - `ALKAROS.Host.Experience.Composition.Tests`: **9/9** (regresyon yok).
  - `ALKAROS.QrOrdering.PendingOrders.Tests`: **13/13** (regresyon yok).
  - `tests/Architecture/ModuleBoundaries`: **8/8** (regresyon yok).
- `python -m pytest tests/Apps/CustomerWeb`: **8/8** (Menu 4 + OrderEntry 4).
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var olan, ilgisiz ihlal (değişmedi), yeni
  ihlal yok.
- **Gerçek Docker Compose ile uçtan uca doğrulama** (`docker compose build/up`, gerçek `alkaros-postgres-1`/
  `alkaros-api-1` konteynerleri, aynı loopback röle topolojisi simülasyonu):
  - Elle oluşturulmuş bir masa + ürün + gerçek token ile: `POST /api/v1/qr/orders` → **202 Accepted**, masa
    durumu **Reserved**'e geçti, outbox'a tam olarak 1 satır kuyruklandı.
  - `GET /api/v1/qr/orders/{submissionId}` polling: gerçek arka plan outbox dispatcher'ının Order'ı Draft →
    Submitted → PendingConfirmation'a taşıdığı, birkaç saniye içinde gözlemlendi.
  - **Bu doğrulama sırasında gerçek bir istemci-taraflı hata bulundu ve düzeltildi:** `order-entry.js`'in ilk
    hâli, poll yanıtındaki durum "Pending" değilse hemen sonlandırıyordu — ama gerçek arka plan işleyicisi
    kısa bir an için "Draft"/"Submitted" durumlarından geçiyor, ve bu durumlar da "Pending" değil, bu yüzden
    poll döngüsü müşteriye "PendingConfirmation" hiç görünmeden, "Draft" durumunda donmuş bir mesajla erken
    duruyordu. `isStillMaterializing(status)` eklenerek düzeltildi (yalnızca "Pending"/"Draft"/"Submitted"
    poll'a devam ettiriyor); testte kilit altına alındı
    (`test_customer_web_order_entry_javascript_submission_flow`).
  - Test verisi (masa, ürün, token, sipariş, kitchen ticket) doğrulama sonrası veritabanından silindi.

## Handoff

- V12-QRO-003
- V12-OUI-001

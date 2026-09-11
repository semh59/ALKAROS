# V1-RMD-162 - Sınırsız girdi ve Orders'ın PostgresException eşlemesi

- Task ID: V1-RMD-162
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Goal

`docs/engineering/garson-audit-2026-09-10.md`'nin API uç noktaları
bölümünden iki bulguyu kapatır (aynı kökten, birlikte ele alındı):

1. **Miktar, kalem sayısı ve not uzunluğu sınırsız; taşma kalıcı hatayı
   503 diye gösterip kuyruğun sonsuza kadar tekrar denemesine yol
   açıyor.** `orders.order_items.quantity` NUMERIC(18,3) ve `notes` TEXT
   sütunlarının kendisi bir üst sınır dayatmıyor — Postgres her ikisini de
   büyük değerlerle sessizce kabul ediyordu; tek sınır V1-RMD-146'nın alt
   sınırıydı (`MinimumOrderQuantity`). Yeni `ValidateRequestBounds`,
   `CreateOrUpdateTableDraftAsync`'in en başında, veritabanına hiç
   dokunmadan: kalem sayısı (≤200), sipariş notu uzunluğu (≤1000), her
   kalemin miktarı (≤9999) ve özel talimat uzunluğu (≤1000), her eklenti
   seçiminin miktarı (≤9999) kontrol ediyor. Sınırlar cömert — gerçek bir
   mutfak siparişinin asla yaklaşmayacağı değerler.
2. **Her `PostgresException` 503'e eşleniyor.** Orders'ın hata
   eşleyicisinde hiçbir `PostgresException`'a özel dal yoktu (Catalog'un
   aksine) — bu, madde 1'in "taşma kalıcı 503 gösteriyor" kısmıyla aynı
   kök: uygulama seviyesinde yakalanmayan bir veri kısıtı ihlali (ör.
   V1-RMD-156'nın CHECK kısıtları) ham bir Postgres hatası olarak
   sızıyor ve generic 503'e düşüyordu — istemciye "veritabanı
   erişilemez" yalanı söylüyor, çevrimdışı kuyruğun asla başaramayacak
   bir isteği sonsuza kadar tekrar denemesine yol açıyordu.
   `UniqueViolation` → 409, `ForeignKeyViolation` → 400,
   `CheckViolation`/`NumericValueOutOfRange` → 400 dalları eklendi,
   Catalog'un kendi eşleyicisinin zaten kullandığı desenle aynı.

## Owned surface

- `plan/v1/remediation/V1-RMD-162-api-input-bounds-and-error-mapping.md` (yeni)
- Sınırlı ek — aşağıdaki tüm yollar ilgili görevin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - src/Host/Experience/Orders/OrderManagementStore.cs (V1-RMD-147
    sahipliğinde) — yeni sabitler ve ValidateRequestBounds.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (V1-ORD-005
    sahipliğinde) — Map()'e üç yeni PostgresException dalı.
  - tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftHttpTests.cs
    (V1-ORD-006 sahipliğinde) — üç yeni test.

## Out of scope

API uç noktaları bölümünün kalan dört bulgusu (ayrı görev/görevler):
- Masa yönetimi/push uç noktalarında hız sınırı; `/pending`'in kovası.
- Katalog sayfalama imleci.
- `X-Idempotency-Key` başlığının okunması.
- `/comp` ve `/transfer-server`'ın istemcisi.

## Dependencies

- V1-RMD-161

## Acceptance evidence

- `dotnet build src/Host/ALKAROS.Host.csproj -c Debug` → 0 uyarı, 0 hata.
- `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres dotnet test`
  gerçek test Postgres'ine karşı:
  - `tests/Host/Experience/Orders/TableDraft` — **48/48 yeşil** (45
    mevcut + 3 yeni: miktar aşımı reddi, not uzunluğu aşımı reddi, kalem
    sayısı aşımı reddi).
  - `tests/Host/Experience/Orders/{Comp,Confirmation,Void,VoidSent}` —
    toplam 46/46 yeşil.
- Üç yeni testin **vacuous olmadığı kanıtlandı**: `git stash` ile
  `OrderManagementStore.cs` değişikliği geri alınıp yalnız o üç test
  çalıştırıldı → üçü de gerçekten düştü (`Expected: BadRequest, Actual:
  OK`). Değişiklik geri yüklendi, tekrar 48/48 yeşil.
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → bu görevin
  değiştirdiği dosyalarda 0 ihlal. (Kalan tek ihlal,
  `src/Modules/Inventory/ManualAdjustments/InventoryAdjustmentService.cs:96`,
  bu görevden önce vardı ve sahip olunan yüzeyin dışında.)

## Handoff

- None

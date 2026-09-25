# V12-STK-001 - Implement cross-channel last-portion arbitration

- Task ID: V12-STK-001
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.34-I.37
- PDF:II.2.19
- PDF:II.7.4
- PDF:III.22

## Goal

Cashier, waiter, QR ve online channel için tek channel-neutral reservation command ve ortak last-portion arbitration
sonucu sağlamak.

## Owned surface

- `src/Modules/Inventory/CrossChannelReservation/**`, `tests/Modules/Inventory/CrossChannelReservation/**`
- `evidence/V12-STK-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Kapsam kararı (Semih, 2026-09-25, AskUserQuestion — "Hold + tüketim guard'ı tek görevde"):
  bugün her kanalın kabulü stoğu doğrudan eldeki miktardan düşüyor (V1-RMD-143) ve bu
  düşüm rezervasyonları görmüyordu; kanallar arası son porsiyon koruması ancak tüketim
  tarafı da hold'lara saygı gösterirse gerçek olur. Bu yüzden aşağıdaki sınırlı ekler bu
  görevin aynı commit'inde yapıldı.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan yazıldı):
  - src/Modules/Inventory/InventoryModule.cs (V11-RMD-002 sahipliğinde) — yalnız iki yeni
    kayıt: kanallar arası hakem ve tüketim guard'ı.
  - src/Host/Experience/Orders/OrderStockConsumption/OrderStockConsumptionService.cs
    (V1-RMD-143 sahipliğinde) — yeni kurucu bağımlılığı ve ürün ile modifier tüketiminden
    hemen önce guard çağrısı; kilit sırası, eşleme ve hareket kaydı değişmedi.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (V1-ORD-005 sahipliğinde) ve
    src/Host/Experience/NfcOrdering/NfcOrderingEndpoints.cs (V12-NFC-001 sahipliğinde) —
    yalnız guard için birer TryAdd kaydı.
  - tests/Host/Experience/Orders/Confirmation/ (V1-RMD-137 sahipliğinde): iki yeni HTTP
    testi, iki yeni fixture yardımcısı, QrOrderExpiryHostedServiceTests içinde kurucu
    çağrısına guard eklendi.
  - tests/Host/Experience/Orders/Confirmation, Orders/TableDraft, Orders/VoidSent,
    NfcOrdering ve QrOrdering test projeleri — yalnız 064 ve 065 migration fixture
    satırları (guard, kabul yolunda portion_reservations tablosunu okur; üretimde bu
    tablolar zaten var).
  - tools/consistency-audit/unreachable_services_allowlist.json (V1-RMD-273 sahipliğinde) —
    hakem için iki satır; ilk çalışma zamanı çağıranı V12-QRO-003 bağladığında silinir.
  - ALKAROS.slnx — yalnız yeni test projesi satırı.

## In scope

- Kanaldan bağımsız komut, eşzamanlılık sonuç eşlemesi ve provider reddetme telafisi.
- Kanal reddi/iptal telafisi V12-ONL-003 status sync sözleşmesi üzerinden yapılır; bu task yalnız rezervasyon sonucunu
  üretir.

## Out of scope

- Rezervasyon yaşam döngüsü dahili bileşenleri ve provider status aktarımı.

## Dependencies

- V11-RSV-002
- V11-RSV-003
- V0-YSP-001

## Deliverables

- `src/Modules/Inventory/CrossChannelReservation/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Tek bölümlü paralel dört kanallı test, bir rezervasyon ve üç açık OutOfStock/red sonucu verir.
- Online iptal sonucu V12-ONL-003 üzerinden tüketilir; Release/Waste yaşam döngüsü kararı yalnız V11-RSV-003
  kanıtıyla doğrulanır ve bu task aynı etkileri yeniden üretmez.
- Kapanış kanıtı (2026-09-25, gerçek PostgreSQL 18 UTF8, port 56433): yeni test projesi 27/27 yeşil. Dört kanal
  aynı anda tek porsiyonu ister; bir `Reserved`, üç tipli `OutOfStock` sonucu çıkar. Bakiye 1/1/0 olur ve tek
  rezervasyon satırı yazılır. Telafi, iptal kararını V11-RSV-003'ün `PortionCancellationDecisionService`'ine
  bırakır: mutfak başlamadıysa Release, başladıysa Waste; tekrar çağrı etkiyi kopyalamaz. V11-INV-007 drift
  kontrolü bu fark için temiz çıkar.
- Tüketim tarafı: kasa kabulü başka bir siparişin hold'unu alamaz (HTTP 409 `INSUFFICIENT_STOCK`); siparişin kendi
  hold'u kabulde `Consumed` olur. İki yeni Host testiyle kanıtlandı (Confirmation 21/21).
- Mutasyon kontrolü (her biri geri alındı, dosyalar birebir eşleşti): satır kilidi kaldırılınca 2 test,
  guard'ın bakiye kontrolü kaldırılınca 3 test, kendi hold'unu çevirme kapatılınca 2 test, replay kontrolü
  kaldırılınca 3 test, kabul yolundaki guard çağrısı atlanınca 2 Host testi kırmızıya döndü.
- Regresyon: Host.Tests (MigrationComposition) 161/161, Confirmation 21/21, TableDraft 84/84, VoidSent 14/14, NfcOrdering 19/19, QrOrdering 32/32,
  Inventory rezervasyon paketleri 9+9+9+13 yeşil, `ModuleBoundaries` 9/9, çözüm derlemesi 0 uyarı 0 hata.
- Bilinen sınır: hakemin ilk çalışma zamanı çağıranı henüz yok (V12-QRO-003 ve V12-ONL-002 bağlayacak), bu
  yüzden reachability izin listesinde görev referansıyla duruyor. Waived `V0-YSP-001` kenarı `V12-GOV-004`
  kapsamında; gerçek sağlayıcı kanıtı iddia edilmez.
- Kanıt: `evidence/V12-STK-001/`.

## Handoff

- V12-QRO-003
- V12-ONL-002

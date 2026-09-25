# V12-QRO-003 - Implement QR confirmation and portion reservation

- Task ID: V12-QRO-003
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.34-I.37
- PDF:II.2.18
- PDF:II.6.8
- PDF:II.7.3
- PDF:III.21

## Goal

Bekleyen bir QR order'yi onaylayın veya reddedin ve bölümleri yalnızca başarılı kabul üzerine ayırın.

## Owned surface

- `src/Modules/QrOrdering/Confirmation/**`, `tests/Modules/QrOrdering/Confirmation/**`
- `evidence/V12-QRO-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Düzeltilen yol notu: yukarıdaki iki dizin kullanılmadı ve boş kaldı. QR Ordering modülünün
  onaylı doğrudan çağrı kenarları yalnız Identity ve Table Management (V0-ARC-001 satır 19);
  Order ve Inventory'ye erişemez. QR siparişinin onay/ret akışı zaten Host'taki
  PendingOrderConfirmationStore'da yaşıyor (V1-RMD-137, sınıf yorumu bu görevi açıkça
  bekliyordu). Semih 2026-09-25'te "Sınırlı ek + yol notu" yolunu seçti.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan yazıldı):
  - src/Host/Experience/Orders/PendingOrderConfirmation/PendingOrderConfirmationStore.cs
    (V1-RMD-137 sahipliğinde) — QR kabulünde kanallar arası hakem üzerinden porsiyon
    talebi; tüketim, Accepted yazımı ve masanın Occupied geçişi tek transaction'da; ret
    yazımı ile masanın serbest kalması tek transaction'da.
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs (V1-ORD-005 sahipliğinde) —
    hakem zinciri için TryAdd kayıtları ve bir hata eşlemesi satırı.
  - tests/Host/Experience/Orders/Confirmation/ (V1-RMD-137 sahipliğinde) — yeni
    QrConfirmationReservationHttpTests.cs dosyası, fixture yardımcıları ve
    QrOrderExpiryHostedServiceTests içinde kurucu çağrısı.
  - tools/consistency-audit/unreachable_services_allowlist.json (V1-RMD-273 sahipliğinde)
    — artık erişilebilir olan 14 satır silindi (V12-STK-001'in iki satırı ve V1-RMD-278'in
    bilinçli olarak bağlamadığı rezervasyon ailesi). V1-RMD-278'in çift düşüm gerekçesi
    V12-STK-001'in tüketim guard'ıyla kapandı: hold, tüketimde Consumed olur.

## In scope

- İzin, satır sürümü, atomik Order geçişi, table politikası ve rezervasyon komutu.

## Out of scope

- Röle güvenliği ve mutfak hazırlama davranışı.

## Dependencies

- V12-QRO-001
- V12-QRO-002
- V11-RSV-002
- V1-IAM-002
- V12-STK-001
- V1-FND-005

## Deliverables

- `src/Modules/QrOrdering/Confirmation/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Onay, bir Kabul Edilen Order ve atomik olarak rezervasyon oluşturur; ret/stok kaybı hiçbir rezervasyon veya kısmi
  table durumu bırakmaz.
- Kapanış kanıtı (2026-09-25, gerçek PostgreSQL 18 UTF8, port 56433): yeni QrConfirmationReservationHttpTests 7/7
  yeşil, Confirmation paketi toplam 28/28. QR kabulü, `Qr` kanal etiketli ve gönderim kimliğini taşıyan bir hold
  yazar; aynı transaction'da hold tüketime (`Consumed`) dönüşür ve masa `Occupied` olur. Son porsiyonu başka bir
  kanal tutuyorsa kabul 409 `INSUFFICIENT_STOCK` ile reddedilir: sipariş `PendingConfirmation`, masa `Reserved`
  kalır ve hiç hold yazılmaz. Eşlenmemiş ürün, eski satır sürümü ve yetkisiz oturum da hold bırakmaz. Aynı QR
  siparişini iki personel aynı anda kabul ederse bir 200 ve bir 409 döner; stok tek kez düşer ve tek hold kalır.
  Ret, masayı serbest bırakır ve stoğa hiç dokunmaz.
- Mutasyon kontrolü (geri alındı, dosya birebir eşleşti): QR hakem çağrısı atlanınca 2 test, kabul
  transaction'ı commit edilmeyince 9 test kırmızıya döndü.
- Kanıt: `evidence/V12-QRO-003/`.

## Handoff

- V20-UAT-001

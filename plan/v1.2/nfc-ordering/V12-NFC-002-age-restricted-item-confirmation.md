# V12-NFC-002 - Route age-restricted NFC orders through staff confirmation

- Task ID: V12-NFC-002
- Status: Done
- Assignee: claude-session-01KUpNDVPb45EwMYysqeu1wc
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-07

## Goal

`V12-NFC-001`'in her zaman doğrudan `Accepted`e geçen güvenilir-kanal
kısayolunu, sepette yaş kısıtlı bir ürün olduğunda devre dışı bırakmak:
böyle bir sipariş de (kanaldan bağımsız kural — `docs/design/modules/qr-nfc-ordering.md`
§4) masayı yalnız `Reserved` yapar ve `PendingConfirmation`da bekler, tıpkı
QR siparişi gibi.

## Owned surface

- `database/migrations/V12/V12-NFC-002/**` (yeni — `catalog.products`e
  `is_age_restricted` alanı; bu alan bugün hiçbir yerde yok).
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - src/Host/Experience/NfcOrdering/** (V12-NFC-001 sahipliğinde) — aynı
    store'un genişletilmesi.
  - src/Modules/Catalog/** (ilgili Catalog görevinin sahipliğinde) —
    yalnız is_age_restricted alanının okuma/yazma contract'ı.

## In scope

- `catalog.products.is_age_restricted` alanı (varsayılan `false`).
- `V12-NFC-001`'in "trusted immediate accept" kısayolunun, sepette en az
  bir yaş kısıtlı ürün olduğunda atlanması; sipariş `PendingConfirmation`da
  bırakılması, masanın `Reserved` olması.

## Out of scope

- Personel onay/ret HTTP aksiyonunun kendisi — **2026-09-08'de
  netleştirildi:** `V12-NFC-003` sonradan farklı bir kapsamla (müşteri
  menü/sipariş sayfası) kapandığı için, bu bekleyen siparişi onaylayacak
  kanaldan bağımsız aksiyon `V12-QRO-003`'ün (QR onay/rezervasyon) kapsamına
  girer — o, hangi kanaldan geldiğine bakmaksızın herhangi bir
  `PendingConfirmation` siparişini onaylar/reddeder. Bu görev yalnız "ne
  zaman bekletilir" kararını uygular; şu an bekleyen bir yaş-kısıtlı NFC
  siparişinin onaylanacağı gerçek aksiyon henüz yok (aşağıdaki kanıt
  senaryoları bu yüzden yalnız "beklemede kalır" durumunu doğruluyor).
- QR kanalının kendisi.

## Dependencies

- V12-NFC-001

## Deliverables

- `is_age_restricted` migration'ı (ileri/geri).
- `NfcOrderingStore`'un genişletilmiş versiyonu ve testleri.

## Acceptance evidence

- Sepette yaş kısıtlı ürün olan bir NFC siparişi `Accepted`e hiç geçmez
  (`PendingConfirmation`da kalır), masa `Reserved` olur, `Occupied` olmaz —
  hem tek ürünlü hem karışık (bir yaş-kısıtlı + bir normal ürün) sepet için
  test edildi.
- Aynı gönderimin tekrarı (idempotent replay) aynı bekleyen siparişi
  döndürür, ikinci bir mutfak bileti veya ikinci bir sipariş oluşturmaz.
- Sepette yaş kısıtlı ürün olmayan bir NFC siparişi `V12-NFC-001`'deki
  gibi davranmaya devam eder (regresyon yok — mevcut 9 test değişmeden
  geçti).
- `dotnet build ALKAROS.slnx`: 0 hata/0 uyarı.
  `ALKAROS.Host.Experience.NfcOrdering.Tests`: 12/12 (9 eski + 3 yeni),
  yerel Postgres'e (55432) karşı.
- Migration 084 (`catalog.products.is_age_restricted`): ileri yönde yeni
  sütun varsayılan `false` ile eklendi; geri yönde `MigrationExecutionTests`
  ve tam `ALKAROS.Host.Tests` (121 test) suite'i tarafından dolaylı olarak
  kapsanan standart migration-manifest doğrulama zincirine dahil edildi.

## Handoff

- None

# V14-NFC-002 - Route age-restricted NFC orders through staff confirmation

- Task ID: V14-NFC-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-07

## Goal

`V14-NFC-001`'in her zaman doğrudan `Accepted`e geçen güvenilir-kanal
kısayolunu, sepette yaş kısıtlı bir ürün olduğunda devre dışı bırakmak:
böyle bir sipariş de (kanaldan bağımsız kural — `docs/design/modules/qr-nfc-ordering.md`
§4) masayı yalnız `Reserved` yapar ve `PendingConfirmation`da bekler, tıpkı
QR siparişi gibi.

## Owned surface

- `database/migrations/V14/V14-NFC-002/**` (yeni — `catalog.products`e
  `is_age_restricted` alanı; bu alan bugün hiçbir yerde yok).
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır
  (yollar geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası
  olarak parse etmesin):
  - src/Host/Experience/NfcOrdering/** (V14-NFC-001 sahipliğinde) — aynı
    store'un genişletilmesi.
  - src/Modules/Catalog/** (ilgili Catalog görevinin sahipliğinde) —
    yalnız is_age_restricted alanının okuma/yazma contract'ı.

## In scope

- `catalog.products.is_age_restricted` alanı (varsayılan `false`).
- `V14-NFC-001`'in "trusted immediate accept" kısayolunun, sepette en az
  bir yaş kısıtlı ürün olduğunda atlanması; sipariş `PendingConfirmation`da
  bırakılması, masanın `Reserved` olması.
- Bu bekleyen NFC siparişleri için de aynı `V14-NFC-003`'ün (henüz
  planlanmadı) sağlayacağı genel, kanaldan bağımsız personel onay/ret
  aksiyonunun kullanılabilir olması (bu görev yalnız "ne zaman bekletilir"
  kararını uygular, onay aksiyonunun kendisini değil).

## Out of scope

- Personel onay/ret HTTP aksiyonunun kendisi — QR ve NFC'nin ortak
  ihtiyacı; ayrı bir görev (`V14-NFC-003` veya `V14-QRO-003` ile
  paylaşılan bir aksiyon — henüz karar verilmedi, bu görev başlarken
  netleştirilecek).
- QR kanalının kendisi.

## Dependencies

- V14-NFC-001

## Deliverables

- `is_age_restricted` migration'ı (ileri/geri).
- `NfcOrderingStore`'un genişletilmiş versiyonu ve testleri.

## Acceptance evidence

- Sepette yaş kısıtlı ürün olan bir NFC siparişi `Accepted`e hiç geçmez,
  masa `Reserved` olur, `Occupied` olmaz.
- Sepette yaş kısıtlı ürün olmayan bir NFC siparişi `V14-NFC-001`'deki
  gibi davranmaya devam eder (regresyon yok).
- `dotnet build` ve ilgili testler gerçek Docker container exit code `0`
  ile geçer.

## Handoff

- None

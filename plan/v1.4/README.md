# V1.4 - QR and Online Ordering

## Hedef

QR, NFC ve Yemeksepeti siparişlerini güvenli biçimde ortak Order ve
PortionInventory domain'ine bağlamak.

## Giriş koşulu

`GATE-V14-ENTRY` koşulu plan/GATES.md'de tanımlıdır; dış sözleşme sahipleri (V0-YSP-001, V0-QRG-001) açık Blocked
olduğu sürece giriş koşulu sağlanmaz. **Bu blok koşulu yalnız o dış sözleşmelere gerçekten bağımlı modülleri kapsar**
(`qr-ordering`, `qr-security`, `qr-transport`, `online-ordering`, `channel-mapping`, `customer-web`,
`reconciliation`, `reporting`, `shared-stock`). `nfc-ordering` modülü bu dış sözleşmelerin hiçbirine bağımlı değildir
(yalnız `Done` olan `V1-ORD-*`/`V1-TBL-*`'ye bağımlıdır, restoranın kendi yerel ağı üzerinden çalışır, public relay
veya ödeme provider'ı gerektirmez) — bu istisna `V14-GOV-001` ile kaydedildi ve `nfc-ordering` görevleri
`GATE-V14-ENTRY` beklemeden başlayabilir.

## Çıkış kapısı

- Bu sürüm altındaki 20 görev dosyasının tamamı `Done`.
- Public token, replay, abuse ve rate-limit testleri geçer.
- PendingConfirmation masa davranışı kilitli ve concurrency-safe olur.
- Duplicate webhook duplicate order veya stok tüketimi üretmez.
- Restaurant/online son porsiyon yarışında yalnızca bir kanal kazanır.
- Catalog ve availability publish işlemleri provider sandbox'ında idempotent ve
  iç durumla mutabık kanıtlanır.
- QR customer web ve online operations UI yalnız sahip domain contract'larını
  çağırır; doğrudan state veya stok yazmaz.

## Modüller

`channel-mapping`, `customer-web`, `governance`, `nfc-ordering`,
`online-operations-ui`, `online-ordering`, `qr-ordering`, `qr-security`,
`qr-transport`, `reconciliation`, `reporting`, `shared-stock`.

Doğrulanan plan hacmi: 12 modül, 24 tek-sahip görev dosyası. 2026-09-07:
`V14-GOV-001` (NFC kanalının kabulü ve giriş koşulu netleştirmesi) ve
`V14-QRT-002` (relay provider/onboarding kararı) `Done`; `V14-NFC-001`
(NFC self-check-in + güvenilir sipariş, gerçek Docker test kanıtıyla)
`Done`; `V14-NFC-002` (yaş kısıtlı ürün istisnası) `Planned`. Kalan 20
orijinal görev hâlâ `Planned`, `GATE-V14-ENTRY`ye tabi.

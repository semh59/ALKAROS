# V1.4 - QR and Online Ordering

## Hedef

QR ve Yemeksepeti siparişlerini güvenli biçimde ortak Order ve PortionInventory
domain'ine bağlamak.

## Giriş koşulu

`GATE-V14-ENTRY` ile uygulanacak QR/Yemeksepeti sözleşmeleri kapanmış olmalıdır.

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

`channel-mapping`, `customer-web`, `online-operations-ui`, `online-ordering`,
`qr-ordering`, `qr-security`, `qr-transport`, `reconciliation`, `reporting`,
`shared-stock`.

Doğrulanan plan hacmi: 10 modül, 20 tek-sahip görev.

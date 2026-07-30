# V1.4 - QR and Online Ordering

## Hedef

QR ve Yemeksepeti siparişlerini güvenli biçimde ortak Order ve PortionInventory
domain'ine bağlamak.

## Giriş koşulu

V1.3 çıkış kapısı ve V0 QR/Yemeksepeti sözleşmeleri kapanmış olmalıdır.

## Çıkış kapısı

- Bu sürüm altındaki 19 görev dosyasının tamamı `Done`.
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
`qr-ordering`, `qr-security`, `reconciliation`, `reporting`, `shared-stock`.

Doğrulanan plan hacmi: 9 modül, 19 tek-sahip görev.

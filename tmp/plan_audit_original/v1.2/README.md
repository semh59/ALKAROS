# V1.2 - Payment, Fiscal, Cash and Meal Card

## Hedef

Para hareketlerini, allocation ledger'ını, Hugin T300 mali akışını, kasayı ve
meal card mutabakatını güvenli biçimde çalıştırmak.

## Giriş koşulu

V1.1 çıkış kapısı ve V0 Hugin sözleşmesi kapanmış olmalıdır.

## Çıkış kapısı

- Bu sürüm altındaki 22 görev dosyasının tamamı `Done`.
- Split payment ve bill closure invariant'ları otomatik testlerle kanıtlanır.
- Timeout/unknown/refund yolları gerçek T300 sandbox veya cihaz çıktısıyla geçer.
- Kısmi iade allocation seviyesinde izlenebilir.
- CashSession ve meal card settlement farkları reconciliation üretir.

## Modüller

`cash`, `fiscal`, `hugin-t300`, `meal-card`, `payment-allocation`, `payments`,
`payments-ui`, `reconciliation`, `reporting`.

Doğrulanan plan hacmi: 9 modül, 22 tek-sahip görev.

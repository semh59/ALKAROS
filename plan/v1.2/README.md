# V1.2 - Payment, Fiscal, Cash and Meal Card

## Hedef

Para hareketlerini, allocation ledger'ını, Hugin T300 mali akışını, kasayı ve
meal card mutabakatını güvenli biçimde çalıştırmak.

## Giriş koşulu

`GATE-V12-ENTRY` ve uygulanacak Hugin/meal-card private sözleşmeleri kapanmış
olmalıdır.

## Çıkış kapısı

- Bu sürümdeki 30 sabit görev ve her approved meal-card provider için türetilen
  bir `V12-MCD-1xx` görevi `Done` veya tarihli/onaylı koşullu kapsam için
  `NotApplicable` olmalıdır.
- Split payment ve bill closure invariant'ları otomatik testlerle kanıtlanır.
- Timeout/unknown/refund yolları gerçek T300 sandbox veya cihaz çıktısıyla geçer.
- Kısmi iade allocation seviyesinde izlenebilir.
- CashSession ve meal card settlement farkları reconciliation üretir.

## Modüller

`cash`, `fiscal`, `hugin-t300`, `meal-card`, `payment-allocation`, `payments`,
`payments-ui`, `reconciliation`, `reporting`, `table-payment`.

Doğrulanan plan hacmi: 10 modül, `30 + approved meal-card provider count`
tek-sahip görev.

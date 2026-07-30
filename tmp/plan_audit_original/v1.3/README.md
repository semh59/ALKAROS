# V1.3 - Customer Account and Invoicing

## Hedef

Cari hesap, dönemsel faturalama, QNB e-belge ve gelen fatura/satın alma akışını
tamamlamak.

## Giriş koşulu

V1.2 çıkış kapısı ve V0 QNB sözleşmesi kapanmış olmalıdır.

## Çıkış kapısı

- Bu sürüm altındaki 20 görev dosyasının tamamı `Done`.
- Cari bakiye transaction ledger'dan yeniden üretilebilir.
- Faturalama charge değerini ikinci kez borç yazmaz.
- QNB outgoing/incoming ve timeout reconciliation akışları geçer.
- KVKK veri yaşam döngüsü müşteri tabloları dışındaki PII alanlarını da kapsar.

## Modüller

`accounts-ui`, `customer-account`, `customer-data`, `invoicing`, `purchasing`,
`qnb-esolutions`, `reporting`.

Doğrulanan plan hacmi: 7 modül, 20 tek-sahip görev.

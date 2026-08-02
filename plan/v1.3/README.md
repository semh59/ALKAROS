# V1.3 - Customer Account and Invoicing

## Hedef

Cari hesap, dönemsel faturalama, QNB e-belge ve gelen fatura/satın alma akışını
tamamlamak.

## Giriş koşulu

`GATE-V13-ENTRY` ve uygulanacak QNB capability'leri için `V0-QNB-001`
sözleşmesi kapanmış olmalıdır.

## Çıkış kapısı

- Bu sürüm altındaki 24 görev dosyası `Done` veya onaylı koşullu kapsam için `NotApplicable`.
- Cari bakiye transaction ledger'dan yeniden üretilebilir.
- Faturalama charge değerini ikinci kez borç yazmaz.
- QNB outgoing/incoming ve timeout reconciliation akışları geçer.
- KVKK veri yaşam döngüsü; müşteri tabloları dışındaki PII alanlarının yaşam döngüsü `V15-KVK-001/002` görevleriyle
  kapanır.

## Modüller

`accounts-ui`, `customer-account`, `customer-data`, `invoicing`, `purchasing`,
`qnb-esolutions`, `reporting`.

Doğrulanan plan hacmi: 7 modül, 24 tek-sahip görev.

# V1.3 - Payment, Fiscal, Cash and Meal Card

## Hedef

Para hareketlerini, allocation ledger'ını, Hugin T300 mali akışını, kasayı ve
meal card mutabakatını güvenli biçimde çalıştırmak.

## Giriş koşulu

`GATE-V13-ENTRY` ve uygulanacak Hugin/meal-card private sözleşmeleri kapanmış
olmalıdır.

**İstisna (V13-GOV-001, 2026-09-17, Semih kararı):** bu iki koşulla hiçbir
teknik bağı olmayan, tamamen saf domain/backend nitelikli bir alt küme —
`V13-PAY-001`, `V13-PAY-002`, `V13-PAY-005` (EFT, provider entegrasyonu
yok), `V13-CSH-001/002/003`, `V13-ALC-001/002/003`, `V13-PUI-002` —
`GATE-V13-ENTRY` kapanmasını beklemeden ilerleyebilir. Emsal:
`V12-GOV-001`/`V12-GOV-002` (NFC/QR kanallarının aynı gerekçeyle kabulü).
Hugin (`V13-HUG-*`), meal card (`V13-MCD-*`), fiscal (`V13-FSC-*`) ve
bunlara geçişli olarak bağımlı her şey (`V13-PAY-003/004`, `V13-ALC-004`,
`V13-REC-001`, `V13-RPT-001`, `V13-TBL-001`, `V13-PUI-001/003/004`) hâlâ
tam giriş koşuluna tabidir.

## Çıkış kapısı

- Bu sürümdeki 30 sabit görev ve her approved meal-card provider için türetilen
  bir `V13-MCD-1xx` görevi `Done` veya tarihli/onaylı koşullu kapsam için
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

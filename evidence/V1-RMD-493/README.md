# V1-RMD-493 - Alış iade faturası

Tedarikçiden gelen iade faturası (UBL-TR `IADE` tipli fatura veya CreditNote) artık reddedilmez: V1-RMD-489 hattıyla `Return` türünde taslak olur, yönetici onayında stoktan düşer. QNB gelen kutusundan çekilen iadeler de atlanmaz, taslak olur.

## Kurallar

- Belge türü `purchasing.purchase_invoices.kind` (`Invoice` / `Return`, migration 178); belgedeki başvuru fatura numarası `referenced_invoice_number` alanında yalnız bilgi olarak saklanır.
- Satır eşleştirme alış faturasıyla aynıdır: tedarikçi kalemi hatırlanır, alışta yapılan eşleştirme iadede otomatik gelir.
- İade onayı tek işlemde: taslak -> Onaylandı (koşullu güncelleme), her satır için korumalı stok düşümü ve `Return` / `Out` stok hareketi (kaynak: fatura kimliği). Depoda stok yetmeyen tek satır bile varsa 409 `INVOICE_NOT_READY` ve hiçbir şey yazılmaz. Mal kabul kaydı yazılmaz.
- Maliyet etkisi: ürün marj raporunun ortalama alış maliyeti alış girişlerinden hesaplanır; iade bu ortalamayı değiştirmez, yalnız stok miktarını düşürür.
- Ekranda iade, listede "(İade)" ve ayrıntıda "İade faturası" olarak görünür; onay düğmesi "Onayla ve stoktan düş" olur.

## Kanıt

- `tests-module.log`: 42 test (yeni: iade ayrıştırma [IADE tipi, CreditNote, başvuru no], düz fatura iade değil, iade onayı stok ve hareket, stok yetmeyen iade, tek satırı yetmeyen iade hiçbir şey düşmez, ikinci onay, QNB iadesi taslak olur).
- `mutation.log`: 13 mutant, hepsi en az bir testi kırdı; dosyalar hash ile geri yüklendi.
- `migration-up-down.log`: 178 ileri, geri, yeniden ileri, ikinci up (idempotent).
- `gercek-deneme.log`: gerçek Host + Postgres: alış onayı 120, IADE yükleme otomatik eşleşti, onayda stok 72, hareket Return/Out, mal kabul kaydı 0, ikinci onay 409, stoktan fazla CreditNote 409 ve taslak kalır.
- `tests-ui.log` (464/464), `typecheck.log`, `lint-build.log`, `tests-architecture-api.log` (5/5), `tests-architecture-modules.log` (9/9), `tests-host.log` (Host test projeleri, manifest dahil).

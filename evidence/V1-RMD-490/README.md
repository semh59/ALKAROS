# V1-RMD-490 - Alış faturası onayı ve stok girişi

Tüm satırları hammaddeye eşleşmiş taslak alış faturası, yönetici onayıyla siparişsiz bir mal kabul kaydına ve stok girişine çevrilir. Maliyet raporu (V1-RMD-487) bu mal kabul satırlarından beslenir.

## Uç noktalar (`purchasing.manage`)

- `POST /api/v1/management/purchasing/purchase-invoices/{id}/approve` gövde `{ locationId }` -> `{ receiptId }`.
- `POST /purchase-invoices/{id}/reject` -> 204.

## Kurallar

- Onay tek işlemde: fatura `Draft`->`Approved` (durum koşullu güncelleme, çift onayı engeller), mal kabul + kalemler, stok bakiyesi ve `PurchaseReceipt` hareketi. Herhangi biri düşerse hiçbiri yazılmaz.
- Miktar = fatura miktarı x satırın çevrim katsayısı (stok biriminde, 4 hane). Birim fiyat = satır net tutarı / stok birimi miktarı (KDV hariç, satır indirimi dahil).
- Mal kabul tarihi = fatura tarihi (İstanbul gün başlangıcı); maliyet raporu "satış gününe kadarki alışlar" kuralını bu tarihe göre uygular.
- Eşleşmemiş satır veya kayıtlı tedarikçisi olmayan fatura onaylanmaz (409 `INVOICE_NOT_READY`); tedarikçi sonradan kaydedilirse onay VKN ile bulur.
- Migration 176: `goods_receipts.order_id` ve `goods_receipt_items.order_line_id` boş olabilir, `goods_receipts.invoice_id` eklendi (fatura başına en çok bir mal kabul); `order_id` ya da `invoice_id` dolu olmalı. Geri alma, faturadan gelen mal kabulleri siler (stok hareketleri defterde kalır, defter yalnız eklenir).
- Siparişsiz mal kabulün okunması için `IGoodsReceiptRepository` boş sipariş/satır kimliğini `Guid.Empty` olarak okur.

## Kanıt

- `tests-module.log`: 27 test (önceki 20 + onay 7: dönüştürülmüş miktar/fiyat/tarih/stok, çift onay, eşleşmemiş satır, kayıtsız tedarikçi, stok hatasında geri alma, red, bulunamayan fatura).
- `mutation.log`: 6 mutant hepsi yakalandı; dosyalar hash ile geri yüklendi.
- `migration-up-down.log`: 176 ileri, geri, yeniden ileri, ikinci up.
- `gercek-deneme.log`: gerçek Host + Postgres: eşleşmemiş onay 409, onay 200, bakiye 120, kalem (120 kg, 5 TL, siparişsiz, faturaya bağlı), siparişsiz mal kabul okunur, ikinci onay 409 ve stok değişmez, red 204, ikinci red 409.
- `tests-architecture-api.log` (5/5), `tests-architecture-modules.log` (9/9), `tests-host.log` (manifest dahil tam Host).

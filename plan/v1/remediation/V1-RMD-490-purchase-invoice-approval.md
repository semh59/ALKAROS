# V1-RMD-490 - Alış faturası onayı ve stok girişi

- Task ID: V1-RMD-490
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Tüm satırları eşleşmiş taslak alış faturasını yönetici onayıyla mal kabul kaydına ve stok girişine çevirmek; maliyet raporu bu girişten beslenir.

## Owned surface

- `plan/v1/remediation/V1-RMD-490-purchase-invoice-approval.md`

## In scope

- Kesin yollar görev başlatılırken yazılır. Siparişsiz mal kabul için `goods_receipts.order_id` ve `goods_receipt_items.order_line_id` boş olabilir hale gelir, kayıt faturaya bağlanır (migration 176); onayda miktar çevrim katsayısıyla stok birimine, birim fiyat KDV hariç stok birimi fiyatına çevrilir; stok hareketi ve bakiye mevcut mal kabul yoluyla aynı işlemde yazılır; çift onay ve eşleşmemiş satırla onay reddedilir; fatura reddi.

## Out of scope

- Ekran (`V1-RMD-491`), QNB gelen kutusu (`V1-RMD-492`), fatura iptal/iade mahsubu.

## Dependencies

- V1-RMD-489

## Acceptance evidence

- Testler, mutasyon kanıtı ve gerçek Host denemesi; çıktılar `evidence/V1-RMD-490/` altındadır.

## Handoff

- V1-RMD-491

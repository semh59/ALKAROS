# V1-RMD-489 - Alış faturası içeri alma ve ürün eşleştirme

- Task ID: V1-RMD-489
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Tedarikçiden gelen UBL-TR e-fatura XML'ini yükleyip taslak alış faturası olarak saklamak; fatura satırlarını tedarikçi bazında hammaddeye eşleştirmek ve eşleştirmeyi hatırlamak. Stok girişi bu görevde yoktur.

## Owned surface

- `plan/v1/remediation/V1-RMD-489-purchase-invoice-intake.md`

## In scope

- Kesin yollar görev başlatılırken yazılır. UBL-TR ayrıştırıcı (fatura no, ETTN, tarih, tedarikçi VKN/unvan, satır kodu/adı/miktar/birim/KDV hariç birim fiyat); `purchasing.purchase_invoices`, `purchase_invoice_lines`, `supplier_item_mappings` tabloları (migration 175); XML yükleme ve eşleştirme uç noktaları (`purchasing.manage`); aynı ETTN ikinci kez reddedilir; tedarikçi VKN ile mevcut tedarikçiye bağlanır, yoksa eşleşmemiş kalır.

## Out of scope

- Stok girişi (`V1-RMD-490`), ekran (`V1-RMD-491`), QNB gelen kutusu (`V1-RMD-492`).

## Dependencies

- V1-RMD-488

## Acceptance evidence

- Testler, mutasyon kanıtı ve gerçek Host denemesi; çıktılar `evidence/V1-RMD-489/` altındadır.

## Handoff

- V1-RMD-490

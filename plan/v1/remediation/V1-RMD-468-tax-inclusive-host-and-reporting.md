# V1-RMD-468 - KDV dahil satır fiyatı: Host, raporlama ve çift ekran

- Task ID: V1-RMD-468
- Status: Planned
- Assignee: Unassigned
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-30

## Goal

`V1-RMD-467` satır hesabını KDV dahil yapar. Bu görev onu varsayan Host ve raporlama kodunu uyumlar: kanal raporunun net
tutarı `subtotal - discount_total` idi (artık brüt); `total - tax_total` olur. Çift ekranın ikram satırı ikinci bir üste
ekleme formülü taşıyor (`birim x (1 + oran)`); brüt = birim x adet olur.

## Owned surface

- `plan/v1/remediation/V1-RMD-468-tax-inclusive-host-and-reporting.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/Channels/ChannelReportService.cs - yalnız net tutar sorguları
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/Channels/ChannelReportModels.cs - yalnız net tutar açıklaması
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenStore.Display.cs - yalnız ikram satırının brüt hesabı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reporting/Channels/Fixtures/ChannelReportTestDatabase.cs - yalnız KDV dahil tohum tutarları
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/Comp/OrderManagementCompTestDatabase.cs - yalnız KDV dahil tohum tutarları
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/Orders/VoidSent/OrderManagementVoidSentTestDatabase.cs - yalnız KDV dahil tohum tutarları
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/KitchenOperations/KitchenOperationsTestDatabase.cs - yalnız KDV dahil tohum tutarları
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Kitchen/PrintQueue/PostgresPrintQueueIntegrationTests.cs - yalnız KDV dahil tohum tutarları

## In scope

- Kanal raporu net = toplam - KDV; çift ekran ikram satırı brütü; ilgili testlerin ve ham SQL tohumlarının 100 TL = 100 TL beklentisi.
- Ödeme, split ve müşteri hesabı Host testlerinden `V1-RMD-467` sonrası kırılanlar gerekçesiyle bu göreve eklenir (plan düzeltmesi).

## Out of scope

- İstemci ekranları (`V1-RMD-469`); fatura hesaplayıcı.

## Dependencies

- V1-RMD-467

## Acceptance evidence

- Testler ve gerçek Host denemesi (online sipariş kanal raporunda net = brüt - KDV); çıktılar `evidence/V1-RMD-468/` altındadır.

## Handoff

- None

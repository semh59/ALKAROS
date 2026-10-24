# qa-mevzuat-uyum - V1-RMD-602 ham rapor (özet biçim)

Kontrol: 15/142 kalem (127 kalem doğrulanamadı: kapsam/süre; migration, WaiterPwa/Cashier istemcileri, E2E, OrderStockConsumptionService, OrderSettlementService, CashierHandoffStore, ShiftSummaryStore).

| No | Önem | Kanıt | Sorun |
| --- | --- | --- | --- |
| M1 | Yüksek | NfcOrderingStore.cs:425, TableDraftService.cs:682, DualScreenStore.Orders.cs:232, DualScreenStore.cs:186 `COALESCE(t.vat_rate, 0)`; 006-catalog.up.sql:45 `tax_profile_id UUID NULL`; Product.cs:17 | Vergi profili olmayan veya pasif ürün sessizce %0 KDV ile siparişe girer (dört sipariş yolu). Kaynak kural: money-tax-business-date.md:22-24, varsayılan %10. Katalog profili zorunlu kılıyor mu doğrulanamadı. Bilinen PO sorusu S1. |
| M2 | Yüksek (karar) | NfcOrderingStore.cs:244-252, 257-273; qr-nfc-ordering.md:68-70; PendingOrderConfirmationStore.cs:23-25 | Yaş kısıtlı NFC siparişi mutfak fişi/stok tüketimi onaydan önce gider (2026-09-09 Semih kararı koda yorum olarak yazılı); tasarım dokümanı "her zaman garson onayına düşer" diyor. Karar-doküman çelişkisi. |
| M3 | Orta | NfcOrderingStore.cs:127-128,206; QrOrderExpiryHostedService.cs:140 (`source = 'Qr'`) | Yaş kısıtlı NFC siparişi PendingConfirmation ve masa Reserved kalır; zaman aşımı yalnız Qr kaynağını temizler. |
| M4 | Orta | NfcOrder.tsx:266-267 | Müşteri her durumda "mutfağa iletildi, afiyet olsun" görür; onay bekleme/garson çağırma mesajı yok. |
| M5 | Orta | PendingOrderConfirmationStore.cs:97-136; OrderManagementEndpoints.cs:858-873 | Yaş kısıtlı onayında "kimlik kontrolü yapıldı" için ayrı alan/iz yok; yalnız OrdersCreate izni. |
| M6 | Orta (doküman sapması) | Order.cs:430-440; SentItemVoidStore.cs:134; void-complimentary-discount-policy.md:97-147 | Kod canlı eşitleme kapalıyken de kalemi Sent yapar ve Sent/Held için stok iadesi yapar; doküman iadeyi yalnız Sent için ve canlı eşitleme açıksa söyler. |
| M7 | Düşük | ItemExceptionHandler.cs:79-81, SentItemVoidStore.cs:118-120 | İptal/ikram notu order_status_history.reason içinde; KVKK runbook anonimleştirme listesinde yok (tablo adı doğrulanamadı). |
| M8 | Düşük | NfcOrderingStore.cs:163 | NFC sipariş numarası UTC saatiyle, kasa yolu İstanbul saatiyle üretilir (kozmetik). |
| M9 | Düşük (doğrulanamadı) | ItemExceptionHandler.cs:224 | Önceden indirimli satırın ikramı için test yok; sapma görünmüyor. |

Temiz: iki fazlı gönderim idempotency (SubmitOrderHandler.cs:83-125,216-239; NFC ux_orders_table_submission), kuruş yuvarlama (OrderMath.cs:10-15), gönderilmiş kalem iptali (sebep zorunlu, bills.void izni, sipariş+stok iadesi+denetim aynı işlemde, SentItemVoidStore.cs:137-147), iptal/ikram denetimi aynı işlemde (ItemExceptionHandler.cs:131-150), ikram temsili, onay kabul/ret aynı işlemde, NFC giriş sınırları, yaş kısıtı sunucuda.

Doğrulanamayan: vergi profili zorunluluğu; Held iptali testleri; nakit/kart iadesi, e-fatura, ÖKC, bahşiş/servis ücreti, yemek kartı bu dilim dışı.

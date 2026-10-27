# V1-RMD-733 - Hata sözleşmesi ve oran sınırı (603 B023, B029)

**Sonuç:** Billing uçları artık nedene özgü kod ve Türkçe mesajla, tek ortak zarfla cevap veriyor; tüm Billing uçları terminal başına okuma/yazma oran sınırına bağlı; yetkisiz ve başka terminalin oturumuyla hiçbir Billing ucu cevap vermiyor (tablo testiyle doğrulandı).

## Bulgu bulgu
- **B023 (doğru):**
  - `GRANT_DENIED` artık ortak zarfta Türkçe ("İndirim isteği reddedildi.").
  - Doğrulama hataları tek genel mesaj yerine nedene özgü kod ve Türkçe mesaj veriyor. İndirim/bahşiş: `IDEMPOTENCY_KEY_INVALID`, `NOTES_TOO_LONG`, `DISCOUNT_REASON_INVALID`, `DISCOUNT_TYPE_INVALID`, `DISCOUNT_VALUE_INVALID`, `TIP_AMOUNT_INVALID`. İade: `REFUND_TEXT_INVALID`, `REFUND_ITEMS_DUPLICATE`, `REFUND_QUANTITY_INVALID`, `REFUND_STOCK_UNAVAILABLE`, `REFUND_ITEM_NOT_ON_BILL`, `REFUND_AMOUNT_INVALID`, `ALLOWANCE_INVALID`, `ALLOWANCE_BELOW_USED`.
  - İptal edilmiş/reddedilmiş siparişten hesap açma 400 yerine 409 `UNSUPPORTED_ORDER_STATE`.
  - `NOT_FOUND` mesajı bulunan şeye göre (hesap, sipariş, iade isteği).
  - Bilgi fişi 404'ü ortak zarfla döner.
  - Kasa istemcisi sunucu mesajını zaten gösteriyor; PosTerminal sabit cümle kullanıyor, kod bağımlılığı yok.
- **B023 yapılmayan:** bozuk JSON/boş gövde hâlâ çerçevenin zarfsız 400'ü (ASP.NET bağlayıcısı uç nokta filtresinden önce cevap veriyor; bunu zarfa çevirmek uygulama genelinde ara katman ister, bu görevin yüzeyi dışında).
- **B029 (kısmen doğru):** `terminal-read`/`terminal-write` politikaları üç Billing grubunun tüm uçlarına yöntem türüne göre bağlandı (kod yerine grup sözleşmesi; test tüm uçları dolaşıp doğruluyor). 401/403 tablo testi 14 uç için eklendi. Terminal süzgeci (billId/paymentId sorgularının terminale göre kısıtlanması) tek işletme tasarımı olduğundan değiştirilmedi.

## Yüzey genişletmesi
Fiş uç noktasının testi (`tests/Host/Experience/PaymentTender/BillReceiptHttpTests.cs`) yüzeyde yoktu; ayrı plan commit'iyle eklendi (+ 734'e kapsam notu).

## Kanıt (logs/)
Host Billing 88/88 (yeni 4), PaymentTender BillReceipt 7/7, ApiConventions 5/5, mutasyon 7/7 KILLED.
Mevcut üç iade testi yeni kodlara göre güncellendi (eski `REFUND_INPUT_INVALID` genel kodu artık yalnız ayrıştırılmamış durumlarda).

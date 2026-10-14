# V1-RMD-526 kanıtı

Eklenen: `/receipt` yanıtında her ödemede `refundedAmount` (ters kayıt toplamı). Basılan bilgi fişi: iade edilen kalemin altında "İade edildi: n adet", ödeme altında "İade -tutar", iade varsa "Net tahsilat" satırı; iade yoksa bu satırlar çıkmaz.

Doğrulananlar: fiş testi 5/5 (sunucu: ödeme başına ters kayıt toplamı), StaticApps 85/85; mutantlar (kalem satırı, ödeme satırı, net satırı kapısı, net hesap, işaret, sunucu toplamı) öldü.

Rapor netlemesi (kod değişikliği yapılmadı, yalnız kayda geçti):
- Ödeme yöntemi/gün sonu raporları iadeyi ters kayıt toplamıyla netler: `PostgresPaymentSettlementReportRepository.cs` ve `BusinessDayTotalsReader.cs` (PaymentRefundNettingTests 30 TL iade → yöntem toplamı 70 TL).
- Kanal (QR/Online) raporu iadeyi netlemiyor; ürün raporunun kısmi iadeyi düşüp düşmediği doğrulanmadı. Gerekirse ayrı görev açılmalı.

Doğrulanmayanlar: gerçek yazıcı çıktısı ve 72 mm genişlik.

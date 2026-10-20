# V1-RMD-655 kanıtı

- Testler: `test-runs.log` (Audit.EventStore 100, temiz derleme, 0 uyarı); Host noktaları `host-sites.log` (OnlineOrdering 179, Orders.Confirmation 30).
- Yeni test `TheSharedWriterSanitizesAndValidatesLikeTheStore`: ortak yazıcı gizli alanı temizler, geçersiz olayı yazmaz.
- Mutasyon: `mutations.log` 4/4 yakalandı (doğrulama atlanması; meta, önceki ve sonraki durumun temizlenmemesi), dosya geri alındı (aynı).
- Değişiklik: mağazanın ekleme gövdesi `AuditEventWriter.WriteAsync`a taşındı; Host'taki iki ham INSERT (`Order.Accepted/Rejected`, `Order.CustomerNoteViewed`) bu yazıcıya geçti, olay adları ve alanlar aynı.
- Kalan risk: Orders ve OnlineOrdering modüllerindeki üç ham INSERT (ItemExceptionHandler, kimlik bilgisi deposu, mağaza durumu servisi) modül bağımlılık kenarı gerektirdiğinden ayrı görev.

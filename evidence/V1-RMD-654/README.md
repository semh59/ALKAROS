# V1-RMD-654 kanıtı

- Test: `test-runs.log` (Host Orders VoidSent, temiz derleme, 0 uyarı); ilk yeşil koşu `green.log`.
- `AVoidWhoseAuditRecordCannotBeWrittenIsNotMade`: `Order.SentItemVoided` veritabanında reddedilince kalem Active kalır. Başarılı yol testi kararın tek kez, sipariş işleminde yazıldığını doğrular.
- Mutasyon: `mutations.log` 3/3 yakalandı, dosya geri alındı (aynı).
- Değişiklik: ham INSERT kalktı (temizleyici ve doğrulama artık uygulanır); olayın sonradan bilinen alanları (mutfak ve hesap sonucu) uç noktanın `order-item.void-sent` olayında kalır.
- Kalan risk: mutfak ve hesap adımları hâlâ ayrı işlemlerde (V1-IAM-027'de kabul edilmiş ödünleşim); diğer ham INSERT noktaları ayrı görev.

# V1-RMD-647 kanıtı

- Testler: `test-runs.log` (Billing testleri, temiz derleme sonrası) ve `green-new.log` (yeni iki test).
- Mutasyon: `mutations.log` 4/4 yakalandı (denetim commit'ten sonra: indirim ve bahşiş; olay adı değişimi: indirim ve bahşiş), dosyalar geri alındı (aynı).
- Aynı anahtarla tekrar isteğin ikinci denetim satırı üretmemesi `ADiscountAndATipEachWriteOneAuditRecordAndARetryWritesNone` ile doğrulandı; eski uç nokta davranışı tekrarda ikinci satır yazıyordu.
- Kırmızı kanıt: yeni imza eski kodda derlenmediği için kırmızı koşu yerine mutasyon kanıtı kullanıldı.
- Kalan risk: sipariş kalemi iptal/ikram, çift ekran ödeme çözümleme ve diğer modüllerin yazımları ayrı görevlerde.

# V1-RMD-722 - Gönderilmiş kalem iptali ve ikramın atomikliği ve izi

**Sonuç:** B009, B010, B013 kapandı; B024'ün transfer-server kısmı kapandı. B014 ve B025 bilerek değiştirilmedi.

- B009: gönderilmiş kalem iptali artık TEK işlemde: sipariş, stok iadesi, mutfak fişi kalemi, hesap satırının Waste'e dönüşmesi ve iki denetim kaydı birlikte yazılır ya da hiçbiri. Biri düşerse kalem iptal olmaz (mutfak pişirir ama kalem iptal görünür durumu bitti). `IBillRepository.SaveAsync` işlem içi aşırı yüklemesi eklendi; `order-item.void-sent` olayı endpoint'ten mağazaya taşındı.
- B010: void-sent ve ikram, istemcinin zaten gönderdiği `IdempotencyKey` ile tekrar oynatılır: yanıtı kaybolan yeniden deneme ilk sonucu alır (409 çakışma yerine), aynı anahtar farklı istekle reddedilir (IDEMPOTENCY_KEY_REUSED). Yeni `OrderCommandReplayStore` (idempotency_keys tablosu, 24 saat, danışma kilidi).
- B013: ikram, kapanmış (Completed/Cancelled/Rejected) siparişte 409 ORDER_CLOSED, ödenmiş/kapanmış hesaptaki kalemde 409 BILL_NOT_MODIFIABLE verir.
- B024: transfer-server artık aynı işlemde `Order.ServerTransferred` denetim kaydı yazar (kim, kimden kime, kaç sipariş).

**Yapılmayan (neden):**
- B014 (kişisel ikram bütçesi UTC günü): kod Semih'in 2026-09-11 kararına uygun (UTC günü, rapor görünümüyle aynı); `money-tax-business-date.md` ise Europe/Istanbul der. Karar-doküman çelişkisi, Semih'e soru olarak kalır (İstanbul'da bütçe sabah 03:00'te sıfırlanıyor).
- B025 (sipariş okuma uçlarında sahiplik süzgeci): ürün kararı (garsonlar birbirinin masasını görebilir mi), değiştirilmedi.
- B024'ün başka garsona atama ve submit-draft kısmı: sipariş durum geçmişi `ChangedBy` taşıyor; ayrı denetim olayı eklenmedi. fire-course aynı şekilde `ChangedBy` ile kayıtlı.
- B009 yan etkisi: hesap satır sürümü yarışı artık bütün iptali geri alır (409); önceden kalem iptal olup hesap tam fiyatta kalabiliyordu.

## Kanıt
`logs/` (voidsent 24/24, comp 20/20, tabledraft 96/96, mimari testler). Mutasyon: `mutate.mjs`, `mutations.log` 8/8 KILLED, kaynak geri yüklendi.

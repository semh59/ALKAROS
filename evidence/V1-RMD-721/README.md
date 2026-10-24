# V1-RMD-721 - NFC siparişinin yaşam döngüsü ve hata yolları

**Sonuç:** B005, B006, B018, B019, B020, B021, B026 kapandı; B007 yapılmadı.

- B005: yaş kısıtlı sipariş için müşteri ekranı "garsonun onayını bekliyor" der (eskiden hep "mutfağa iletildi").
- B006: yaş kısıtlı NFC tutuşu, mutfak henüz hiçbir kalemi başlatmadıysa, QR gibi zaman aşımıyla reddedilir ve masa serbest kalır; mutfak pişirmeye başladıysa garsona bırakılır (pişen yemek iptal edilmez). Neden: o sipariş fişi onaydan önce gittiği için (2026-09-09 kararı) sürekli reddetmek yanlış olurdu. Ret gerekçesi artık "Sipariş zaman aşımına uğradı…".
- B018: commit sonrası stok kurulum hataları (varsayılan konum yok, stok kalemi yok) ve garson duyuru hatası müşteriye 500 vermez; sipariş garsona bekler ve uyarı olarak loglanır.
- B019: aynı gönderim kimliğiyle farklı sepet artık 409 IDEMPOTENCY_KEY_REUSED döner (aynı sepet yeni kalem kimlikleriyle yeniden gönderilirse eski sipariş döner); istemci kimliği sıfırlar ve açıklar.
- B020: sipariş numarasına sipariş kimliğinden 8 karakter eklendi (TBL ile aynı yöntem); çakışma ve 503 riski kalktı.
- B021: NFC hata süzgeci benzersizlik/serileştirme/kilitlenme hatalarını 409 yapar (503 değil), 409 CONCURRENCY_CONFLICT eşlemeleri uyarı olarak loglanır.
- B026: masa kovası 30 → 120/dk, ek olarak cihaz (adres) başına 20/dk; tek cihaz bir masanın kovasını tek başına tüketemez.

**Yapılmayan:** B007 (yaş kontrolü yapıldı kaydı): onaylayan kişi zaten `Order.Accepted` denetim kaydında; ayrı "kimlik kontrol edildi" alanının yasal zorunluluğu doğrulanamadı, ürün/hukuk kararı gerektirir; atlandı. B021'in PostgresException→409 eşlemesi için doğrudan test yok (tetiklemek deterministik değil); kod okumasıyla doğrulandı.

## Kanıt
`logs/` (nfc 28/28 sonrası eklenenlerle, confirmation 32/32, accesshardening 17/17, vitest-nfc 13/13, tsc exit 0). Mutasyon: `mutate.mjs`, `mutations.log` 9/9 KILLED, kaynak geri yüklendi.

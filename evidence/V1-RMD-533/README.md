# V1-RMD-533 kanıtı

- Migration 187: `observability.error_events` (parmak izine göre tek satır, sayaç, durum, ilk/son görülme, şiddet, sentetik işareti, uyarı bağı), `error_occurrences` (parti başına bir satır), `error_event_counters` (düşen/yazılamayan olay sayaçları).
- `ErrorFingerprint`: katman + kategori + biçimi normalize edilmiş ayrıntı (GUID, sayı, tırnaklı metin, uzun onaltılık kimlik çıkarılır).
- `ErrorEventWriter`: sınırlı bellek kuyruğu (5000), parmak izine göre birleştirilmiş toplu yazım (500'lük parti), tek işlemde upsert; şiddet yalnız yükselir; çözülmüş olay tekrarında yeniden açılır; kuyruk dolunca düşen olay sayılır ve sayaç tablosuna yazılır; veritabanı hatası çağırana fırlatılmaz, kayıp `FailedPending` ile sayılır.
- `AlertErrorEventSink`: yeni veya şiddeti artan Error/Critical olay için mevcut uyarı çatısında bir uyarı açar; tekilleştirme anahtarı `error:<parmak izi>`; tekrar eden olay ikinci uyarı açmaz; Info/Warning uyarı açmaz.
- Ayrıntı metni gizlilik koruyucusundan (V1-RMD-549) geçer; kart numarası depoya yazılmaz.
- Test: ErrorEvents 8/8 gerçek Postgres (500 aynı hata = 1 satır, sayaç 500, 1 oluşum satırı; 25 olaydan 10'u kabul, 15 düştü ve sayaç yazıldı); Host manifest testleri 16/16 ve Observability Host testleri 8/8 (migration 187 tüm zincirde uygulanıyor); mutasyon 14/14 öldü (`mutations.log`); proje manifesti VALID; tutarlılık denetimi temiz.
- Plan kabul koşulu 1 (500 oluş): parti başına parmak izi başına tek upsert doğrulandı. "Ana havuzdan 0 bağlantı" tasarımla sağlanır (yazıcı kendisine verilen veri kaynağını kullanır; Host'ta izleme havuzuyla bağlama V1-RMD-534'te); gerçek ölçüm V1-RMD-556'dadır.
- Devredilenler (plan kaydında güncellendi): saklama kataloğuna ekleme, silme işinin zamanlanması ve denetim kaydı V1-RMD-534 kabul koşuludur (Host bağlamasıyla birlikte).
- Yapılmayan: aylık bölümleme (veri hacmi küçük; saklama silicisi V1-RMD-549 yeterli; hacim V1-RMD-556'da ölçülür ve gerekirse bölümleme eklenir).

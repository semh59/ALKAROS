# V1-RMD-575 - Uç noktanın kendi yanıtladığı 5xx'i izleme

Değişiklik (`src/Host/Observability/ServerDetectors/ServerDetectors.cs`): istek süresi ara katmanı (`SlowRequestMiddleware`), yanıt kodu 500 veya üstüyse ve istisna gözlemcisi aynı istek için olay yazmadıysa `api.server_error_response` (Hata) olayı yazar. İstisna gözlemcisi artık istekte "bildirildi" işareti bırakır; böylece çift olay oluşmaz.

Kanıt
- `test-runs.log`: gerçek Postgres ile 9/9 test yeşil (3 yeni: 500 yanıtı bir olay, gözlemcinin bildirdiği istek ikinci olay üretmez, 200/404 olay üretmez).
- `mutations.log`: 4/4 mutant öldürüldü (M1 ilk koşuda yaşadı; sınır testi 500'e çevrilince öldü).
- Ek düzeltme: aynı test projesinin veritabanı hazırlığı `raw_ui_interactions` tablosunu kurmuyordu; retention testi bu yüzden eskiden beri kırıktı. Hazırlığa 191 geçişi eklendi.

Sınırlar (doğrulanamadı)
- Docker yığınında yeniden denenmedi (masa yönetimi 503 senaryosu); kanıt birim düzeyindedir.
- Yeni kategori `api.server_error_response` için ekranda Türkçe etiket yok, "Bilinmiyor" görünür; etiket sözlüğü ayrı görev olarak açılacak.

# V1-RMD-660 — Birim çevirici doğruluğu

Kapatılan bulgular: 596-B001 (aynı türden birimlerde özel katsayı yok sayılıyordu), B004 (CanConvert/Convert uyumsuzluğu), B014 (açılışta dönüşüm yükleme hatası restart'a kadar kalıyordu).

## Sonuç
- `koli → adet = 12` kaydı artık hem doğrudan hem ters yönde (24 adet → 2 koli) uygulanır; kayıtsız birimlerde de çalışır.
- Fiziksel birimi (kg→g) standart tablodan farklı bir katsayıyla kaydetmek reddedilir.
- Açılışta yükleme başarısız olursa arka planda 5 sn'den 1 dk'ya kadar artan aralıkla yeniden denenir; kapatmada durur.

## Kanıt
- red.log: düzeltme öncesi 4 test kırmızı.
- test-runs.log: Measurements 18/18, Recipes Host 19/19, 0 uyarı 0 hata (temiz derleme).
- dep-*.log: bağımlı 22 test projesi yeşil.
- mutations.log: 8 mutant, 8 öldü, 0 hayatta; her mutant sonrası dosya geri yüklendi ve `cmp` ile özdeş doğrulandı. (Mutant "yeniden deneme durmuyor" önce testi kilitledi; teste 5 sn zaman aşımı eklendi, sonra öldü.)

## Kabul edilen kalan risk
POST `/unit-conversions` bellekte kaydı DB yazımından önce yapar; DB yazımı başarısız olursa kural restart'a kadar bellekte kalır, yeniden gönderme düzeltir. Plan "Kapsam dışı" bölümünde yazılı.

# V1-RMD-495 - Alış faturası hattı sağlamlaştırma

Bağımsız taramanın bulduğu alış faturası riskleri kapatıldı.

## Kurallar

- Belge alanı depolama sınırını aşarsa (fatura no > 64, vergi no > 32, birim > 16, miktar/tutar >= 10^14, birim fiyat >= 10^12) belge 400 `INVALID_INVOICE` ile reddedilir. Depolama sırasında bir veri istisnası (SQLSTATE 22xxx) çıkarsa da 500/503 değil geçersiz belge sayılır. QNB gelen kutusunda böyle belge "atlanan" olur ve imleç ilerler; eskiden aynı belge her çekmede çöküp arkasındaki faturaları kilitliyordu.
- Para birimi TRY değilse fatura reddedilir (kur dönüşümü yoktur).
- QNB'nin indirmeyi reddettiği belge, oturum düşmesi ile ayırt edilemediği için hemen atlanmaz: sonraki bir belge başarıyla inerse atlanmış sayılır ve imleç geçer; reddedilen belgeden sonra hiçbir belge inmezse çekme `stoppedEarly` ile durur, imleç o belgenin önünde kalır.
- `goods_receipt_items(stock_item_id)` indeksi eklendi (migration 179): ortalama alış maliyeti ve ürün marj raporu sorguları tablo taraması yapmıyor.
- Ürün marj raporunda satır sınırı aşımı 500 yerine 400 doğrulama hatasıdır; "iade desteklenmiyor" hata metni güncellendi.

## Açık (dış bağımlılık)

- Üretim QNB adresi sözleşmeyle gelmedi; `ALKAROS_QNB_USER_SERVICE_URL` verilmezse bağlantı QNB test adresine gider ve test belgeleri gerçek taslak olur. Canlıya almadan önce bu değişken verilmeli.

## Kanıt

- `tests-module.log`: 52 test (yeni: para birimi, 6 değer sınırı, reddedilen belge iki yönü, depolama veri istisnası). `tests-margin.log`: 14 test (yeni: satır sınırı).
- `mutation.log`: 11 mutant, hepsi en az bir testi kırdı; dosyalar hash ile geri yüklendi (ilk koşuda tutar eşiği mutantı sağ kaldı; eşik NUMERIC(18,4) sınırına düzeltilip test sıkılaştırıldı).
- `migration-up-down.log`: 179 ileri, geri, yeniden ileri, ikinci up (idempotent).
- `gercek-deneme.log`: gerçek Host + Postgres + sahte QNB: iyi/zehirli/USD/reddedilen/iyi dizisi (2 alındı, 3 atlandı, imleç 6005), reddedilen son belgede durma, USD ve 80 karakterli numara 400.
- `tests-host.log`: manifest, hata kataloğu, satın alma/raporlama/kompozisyon Host testleri ve mimari testler.

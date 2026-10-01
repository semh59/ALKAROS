# V1-RMD-487 - Ürün satış maliyeti ve brüt kâr raporu

`GET /api/v1/management/reports/product-margin?from=&to=` (yönetici oturumu + `reports.view`): tarih aralığında (en çok 31 gün) ürün başına satılan adet, ikram adedi, KDV hariç net satış, maliyet, brüt kâr ve kâr yüzdesi.
Salt okunur; hiçbir şey yazmaz.

## Kurallar

- Kaynak: ödenmiş fişlerin `Sale` ve `Complimentary` kalemleri. Fiş günü, fişin açıldığı `Europe/Istanbul` takvim günüdür (onaylı iş günü kararı). Çevrimiçi siparişin fişi olmadığı için hariç; iptal, açık ve yeniden açılmış fişler hariç.
- Net satış: kalemin `net_amount` değeri (ekstralar dahil, KDV hariç). İkram satışa katkı vermez, adet ve maliyetle ayrıca görünür. Fiş düzeyi indirimler ürüne dağıtılmaz; kontrol bloğunda ayrı toplam olarak gösterilir.
- Maliyet: kalemin `Consumption` stok hareketleri eksi geri alınanlar; her stok kalemi için satış gününe kadarki alış girişlerinin ağırlıklı ortalama fiyatı. Ürün bağlantısı ve ekstralar aynı hareketlerden geldiği için bir şey iki kez sayılmaz.
- Alış kaydı olmayan stok kalemi (veya hiç stok hareketi olmayan kalem) sıfır sayılmaz: `unknownCostLines` ile işaretlenir, o ürünün kâr ve yüzdesi boş kalır.

## Kanıt

- `tests-module.log`: 13 test (KDV hariç net ve satış günündeki ortalama maliyet, sonradan gelen alışın etkisiz kalması, ekstra+ürün bir kez sayılır, geri alınan düşüm çıkar, bilinmeyen maliyet işaretlenir [2 durum], ikram, yalnız ödenmiş fiş [3 durum], İstanbul gün sınırı 23:59:59/00:00, kontrol bloğu dengesi, aralık doğrulaması).
- `mutation.log`: 6 mutant (alışlar satış gününe bakmıyor, geri alma çıkarılmıyor, bilinmeyen maliyet sıfır, ödenmemiş fiş dahil, ikram düşüyor, gün filtresi UTC) kırmızı; dosya geri alındı, `fc /b` özdeş.
- `tests-architecture-api.log`, `tests-architecture-modules.log`: API sözleşmesi ve modül sınır testleri yeşil; proje manifesti geçerli.
- `gercek-deneme.log`: gerçek Host + Postgres: oturumsuz 401, giriş sonrası 200; 3 burger (2 + 1 ödenmiş fiş, 1 iptal edilmiş fiş hariç): net 545,46, maliyet 126 (3 × 30 + 4,5 × 8), kâr 419,46 (%76,9), kontrol dengeli; ters aralık Türkçe doğrulama hatası (400).

## Açık kalan

- Yönetim ekranı bölümü `V1-RMD-488`.
- Alış (mal kabul) kaydı girilmeden maliyet "bilinmiyor" görünür; bu beklenen davranıştır.
- Fiş düzeyi indirim/hizmet bedeli ürüne dağıtılmıyor; marj bu tutarlardan önceki satır düzeyi marjdır.

# V1-RMD-489 - Alış faturası içeri alma ve ürün eşleştirme

Tedarikçiden gelen UBL-TR e-fatura XML'i yönetici oturumuyla yüklenir, taslak alış faturası olarak saklanır; fatura satırları tedarikçi bazında hammaddeye eşleştirilir ve eşleştirme hatırlanır. Bu görev stok girişi yapmaz (onay: `V1-RMD-490`).

## Uç noktalar (`purchasing.manage`, yönetici oturumu)

- `POST /api/v1/management/purchasing/purchase-invoices/import` gövde `{ xml }`: ayrıştırır, taslak olarak kaydeder, tedarikçiyi VKN ile bulur, daha önce hatırlanan eşleştirmeleri uygular.
- `GET /purchase-invoices?status=` ve `GET /purchase-invoices/{id}`: liste (eşleşmemiş satır sayısıyla) ve ayrıntı.
- `PUT /purchase-invoices/{id}/lines/{lineId}/mapping` gövde `{ stockItemId, conversionFactor }`: satırı hammaddeye bağlar, tedarikçinin o ürün ve birimi için hatırlar, aynı tedarikçinin diğer taslak satırlarına da uygular.

## Kurallar

- Aynı ETTN ikinci kez alınmaz (409). İade faturası, kredi notu ve fatura olmayan belgeler reddedilir (400). Dış varlık (XXE) çözülmez, DTD yasaktır.
- Birim fiyat = satır net tutarı / miktar (satır indirimi maliyete yansır), KDV hariç.
- Eşleştirme anahtarı: tedarikçi VKN + tedarikçinin ürün kodu (yoksa normalleşmiş ad) + fatura birimi. Birim değişirse eşleştirme taşınmaz.
- Çevrim katsayısı: fatura biriminin 1'i kaç stok birimi eder (koli = 24 gibi). Onay görevinde miktar ve fiyat bununla stok birimine çevrilecek.
- Yalnız taslak fatura eşleştirilebilir; onaylanmış veya reddedilmiş faturaya eşleştirme yayılmaz.
- QNB gelen kutusu aynı `ImportAsync` yoluna beslenecek (`V1-RMD-492`); kaynak alanı `XmlUpload` / `QnbInbox`.

## Kanıt

- `tests-module.log`: 20 test (ayrıştırıcı 11, veritabanı 9 — taslak kaydı, tedarikçi bağlama, ETTN tekrarı, hatırlama [birim ve tedarikçi sınırı], diğer taslaklara yayılma, yeniden eşleştirme, taslak olmayan, geçersiz stok/katsayı, liste sayaçları).
- `mutation.log`: 9 mutant, hepsi en az bir testi kırdı; dosyalar hash ile geri yüklendi.
- `migration-up-down.log`: 175 ileri, geri, yeniden ileri, ikinci up (idempotent).
- `gercek-deneme.log`: gerçek Host + gerçek Postgres: oturumsuz 401, XML yükleme, tekrar 409, iade belgesi 400, bozuk dosya 400, eşleme, sıfır katsayı 400, ikinci faturanın otomatik eşleşmesi, liste.
- `tests-architecture-api.log` (5/5; veri değiştiren uçlar için isteğe bağlı `IdempotencyKey` alanı eklendi), `tests-architecture-modules.log` (9/9), `tests-host.log` (manifest dahil tam Host).

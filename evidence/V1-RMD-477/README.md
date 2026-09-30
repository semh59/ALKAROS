# V1-RMD-477 - Yalnız isteğe bağlı seçeneği olan ürünleri platforma yayımlamak

PO kararı (2026-09-30): isteğe bağlı ekstrası olan ürün, ekstralar olmadan fiyat ve stokla yayımlanır.

## Değişiklik

- `CatalogPublicationService.cs`: `has_modifiers` yalnız zorunlu seçim grubu (`min_selections > 0`) için doğru; yayımlama kuralı sipariş kabul kuralıyla aynı oldu.
  İstemci etiketi ("zorunlu seçimleri platforma aktarılamıyor") zaten buna uyuyordu.

## Kanıt

- `green-online-ordering.log`: CatalogPublishing testleri 16/16. Yeni test: yalnız isteğe bağlı gruplu ürün hatasız yayımlanır ve platforma fiyatla gider. Mevcut "desteklenmeyen ürün" testi zorunlu grupla çalışır
  (test yardımcısı `minSelections` parametresi aldı, varsayılan 1).
- `mutation.log`: koşul kaldırılınca yeni test kırmızı; dosya geri alındı, hash özdeş.

## Açık kalan

- Seçenekler platforma iletilmez (API desteklemiyor); ürün platformda ekstrasız satılır.

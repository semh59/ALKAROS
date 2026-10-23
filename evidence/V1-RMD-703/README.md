# V1-RMD-703 Fiyat değişikliği ve ileri tarihli fiyat

Kapanan bulgular: V1-RMD-600-B002 (fiyat kapatılamıyor), B003 (ileri tarihli ilk fiyat ürünü satıştan düşürüyor), B005 (TRY dışı fiyat).

- `PostgresPricingRepository.AddReplacingEarlierAsync`: tek işlemde, yeni fiyatın başlangıcında hâlâ süren önceki fiyatın bitişi yeni başlangıca çekilir ve yeni fiyat eklenir. `POST /prices` bunu kullanır; fiyat zammı ve "yarından geçerli" fiyat artık 409 almaz. Sonradan başlayan bir fiyatla gerçek çakışma hâlâ 409.
- Fiyat önbelleği yeniden hesabı ve süpürme: başlamış hiçbir TRY satış fiyatı olmayan ürünün başlangıç fiyatı silinmez.
- Satış fiyatı yalnız TRY; başka para birimi 400 ve eklenmez.
- Testler: Host.Experience.Catalog 18/18, Catalog.Pricing 24/24, Catalog.ProductCatalog 82/82 yeşil (logs/). Eski çakışma testi, yeni davranışa göre gerçek çakışma (öncesinden başlayan fiyat) ile güncellendi.
- Mutasyon: bkz. mutations.log (kaynak her turda geri yüklendi ve doğrulandı).

Açık: fiyat ekranında bitiş tarihi ve fiyat düzeltme yok (V1-RMD-600 B012 grubu); fiyat önbelleği yazımı tek işlemde değil (B015, Düşük). Gerçek tarayıcıda doğrulanamadı.

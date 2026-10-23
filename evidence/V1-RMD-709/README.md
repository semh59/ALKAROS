# V1-RMD-709 Ürün bilgilerini düzeltme

Kapanan bulgular: V1-RMD-600 K10 (ürün düzeltme ucu yok; ürün için), K08 (ürün güncellemesi fiyat önbelleğini eski değerle ezebiliyordu).

- Sunucu: `POST /api/v1/management/catalog/products/{id}/details` SKU, ad, kategori, KDV profili, açıklama ve sıra bilgisini düzeltir. KDV profili var olan aktif bir profil olmalı (yoksa/pasifse 400), yinelenen SKU 409, bilinmeyen ürün 404.
- `PostgresProductRepository.UpdateAsync` artık `current_price` yazmaz; fiyat önbelleğini yalnız fiyat ekleme, süpürme ve ürün oluşturma yazar.
- Ekran: ürün ayrıntısında "Ürünü düzenle"; form SKU, ad, KDV profili ve kategori gösterir; fiyat ve stok modu sorulmaz; hata form içinde.
- Testler: Host.Experience.Catalog 21/21, Catalog.ProductCatalog 83/83, Architecture.ApiConventions 5/5, PosTerminal vitest tümü yeşil, tsc exit 0 (logs/).
- Mutasyon: 10/10 + S3b KILLED. S3 mutantı parametresiz yazıldığı için etkisiz kaldı (SURVIVED); doğru biçimiyle (SET ve parametre birlikte) S3b yeniden denendi ve ProductCatalog testi kırıldı. Kaynak her turda geri yüklendi.

Açık: kategori, KDV profili, fiyat ve seçenek düzeltme ve KDV oranı değişikliği (PO kararı S2); ürün tipi/stok modu değişmez. Gerçek tarayıcıda doğrulanamadı.

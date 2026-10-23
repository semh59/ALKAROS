# V1-RMD-702 Ürün KDV profili olmadan açılamaz

Kapanan bulgu: V1-RMD-600-B001 (yeni ürünler için), B009 (ürün formunda KDV ve kategori seçicisi).

- Sunucu: `CatalogManagementStore.CreateProductAsync` var olan ve aktif KDV profili yoksa 400 `VALIDATION_FAILED` döner, ürün yazılmaz.
- İstemci: ürün formunda zorunlu "KDV profili" ve isteğe bağlı "Kategori" seçicisi; seçilmeden "KDV profili seçin." hatası.
- Testler: Host.Experience.Catalog 13/13 yeşil (profilsiz, rastgele ve pasif profil reddi + ürün eklenmediği); vitest catalog 21/21 yeşil; tsc exit 0 (logs/).
- Mutasyon: 5/5 KILLED, kaynak geri yüklendi ve doğrulandı (mutations.log).

Açık risk (kapsam dışı, PO kararı S1/S2): mevcut profilsiz ürünler siparişte hâlâ %0 KDV ile yazılır; sipariş sorguları (5 yer) değişmedi.
Doğrulanamadı: gerçek tarayıcıda görsel kontrol.

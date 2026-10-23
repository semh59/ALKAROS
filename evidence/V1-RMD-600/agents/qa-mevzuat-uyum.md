# qa-mevzuat-uyum - V1-RMD-600 (SHA e00a61c0)

Kapsam: 71/71 kalem tarandı (31 satır satır, 40 desen taraması). Tek dayanak docs/compliance/money-tax-business-date.md.

| No | Önem | Düzey | Kanıt | Sorun | Öneri |
|---|---|---|---|---|---|
| M-001 | Yüksek | Kod okuması | CatalogWorkspace.tsx:192, :217 (`taxProfileId: draft.taxProfileId \|\| null`); sipariş sorguları `COALESCE(t.vat_rate, 0)`: TableDraftService.cs:682, QrPendingOrderStore.cs:340, NfcOrderingStore.cs:425, DualScreenStore.cs:186, DualScreenStore.Orders.cs:232 | Ürün formunda KDV profili seçimi yok; profilsiz ürün siparişte sessizce %0 KDV. Doküman yeme-içme için %10 der. | Varsayılan oran veya zorunlu seçim; sipariş boş profili reddetsin (Semih S1). |
| M-002 | Yüksek | Kod okuması | Aynı 5 sorgu `LEFT JOIN catalog.tax_profiles t ... AND t.active` (TableDraftService.cs:684) | Pasif profile bağlı ürün sessizce %0 KDV'ye düşer. | M-001 ile aynı kök. |
| M-003 | Yüksek | Kod okuması | CatalogManagementEndpoints.cs:73-85 yalnız GET/POST; PostgresTaxProfileRepository.cs:117 UPDATE var ama Host'ta çağrı yok; tohum veri 0 | KDV oranı değiştirilemez; geçerlilik tarihi (valid_from/to) ve oran geçmişi yok; hazır %1/%10/%20 profili yok. | Geçerlilik aralığı + güncelleme/pasifleştirme ucu + tohum profiller (S2). |
| M-004 | Yüksek | Kod okuması | src/Modules/Catalog ve src/Host/Experience/Catalog içinde `is_age_restricted` yok; NfcOrderingStore.cs:127 yalnız NFC kuralı | Yaş sınırı alanı ürün modeli/API/forma eklenmemiş (084 yalnız SQL); kural yalnız NFC'de, QR/masa/online'da yok. | Alanı modele/API'ye/forma ekle; kanal kuralı kararı (S3). |
| M-005 | Orta | Kod okuması | 006-catalog.up.sql:21 `vat_rate NUMERIC(5,2)` CHECK'siz; TaxProfile.cs:12 yalnız 0-100; UI CatalogWorkspace.tsx:199 | %37,5 gibi geçersiz oran kaydedilebilir (doküman 1/10/20). | İzinli oran kümesi + CHECK (S2). |
| M-006 | Orta | Kod okuması | CatalogWorkspace.tsx:192 "Satış fiyatı (TRY)"; ProductV1.CurrentPrice | "KDV dahil" ibaresi yok; kullanıcı hariç fiyat girebilir. | Etiket "KDV dahil fiyat". |
| M-007 | Orta | Kod okuması | CatalogManagementStore.cs:185-199 yalnız AddAsync; Pricing/Repositories.cs:7; 007 `EXCLUDE gist`; UI fiyat formunda effective_to yok | Açık uçlu fiyat varken yeni fiyat çakışma ihlali verir; eski fiyatı kapatan yol yok; ihlalin yüzeyde görünümü çalıştırılmadı. | Önceki açık fiyatı otomatik kapat veya effective_to alanı. |
| M-008 | Orta | Kod okuması | 007 `fk_product_prices_product ... ON DELETE CASCADE`; PostgresProductRepository.cs:195 DELETE | Ürün silinirse fiyat geçmişi silinir; fiyat geçmişi saklama süresi hiçbir dokümanda yok. | RESTRICT + pasifleştirme + saklama süresi (S4). |
| M-009 | Düşük | Kod okuması | TaxProfile.cs:20, 006 modifier tablosu; order_item_modifiers (011) KDV sütunu yok | Modifikatör farkının KDV oranı Catalog'da belli değil; sipariş tarafı okunmadı. | Orders denetiminde bak (S5). |

Temiz: para tipleri numeric(18,2)/(5,2); negatif fiyat DB'de engelli (022, 097); fiyat çakışması EXCLUDE ile engelli, etkin fiyat yarı açık aralık ve arka plan servisi aynı mantık (CatalogPriceRecomputeHostedService.cs); sipariş satırı tax_rate anlık kopya (011:42); Catalog Host testleri 12/12; zorunlu servis ücreti/bahşiş dilimde yok.

Doğrulanamayan: fişte (e-arşiv) oranın aynen yazıldığı (Orders/Billing/Fiscal denetimlerine bırakıldı).
Semih soruları: S1 boş profil varsayılanı; S2 izinli oranlar ve tarihli değişim, %0; S3 yaş sınırı ürün grupları/kanallar; S4 fiyat geçmişi saklama yılı; S5 modifikatör KDV oranı.

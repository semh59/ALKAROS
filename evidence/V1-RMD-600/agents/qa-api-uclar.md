# qa-api-uclar - V1-RMD-600 (SHA e00a61c0)

Kapsam: 71/71 dosya envanterde; 18/18 rota (17 yönetim rotası CatalogManagementEndpoints.cs:55-190 + terminal rotası DualScreenApplication.Endpoints.cs:360) derin okundu. Host.Experience.Catalog testleri 12/12 yeşil (runs kaydı). Baseline: idempotency_allowlist.json:120-155 içinde 9 katalog POST'u (V1-RMD-102) bilinen.
Doküman sapmaları: terminal rotasında imleç `X-Next-Cursor` başlığı + çıplak dizi (DualScreenApplication.Endpoints.cs:372-376); sayfa boyutu terminal 1000, yönetim 50/100; tekrar anahtarı hiçbir katalog ucunda yok (bilinen).

| No | Önem | Kanıt | Sorun | Öneri |
|---|---|---|---|---|
| A-001 | Orta | CatalogManagementStore.cs:185-199, ProductPrice.cs:33-41, 006 (price NUMERIC(18,2)), ToDto 653-661 | CreatePriceAsync 2'den fazla ondalığı reddetmiyor; Postgres yuvarlıyor, yanıt girilen değeri (125.999) döndürüyor. CurrentPrice, PriceDelta, VatRate için de aynı. | Ölçek 2 doğrula, VALIDATION_FAILED. |
| A-002 | Orta | catalogApi.ts:36 | Yönetim ekranı her kaynağı yalnız `?limit=100` ile bir kez çekiyor, nextCursor atılıyor; 100 üstü ürün/kategori/fiyat görünmez. | İmleç bitene kadar sayfala. |
| A-003 | Orta | Rota listesi: PUT/PATCH/DELETE yok; repo UpdateAsync/DeleteAsync var (PostgresProductRepository.cs:150-203) | Kategori, ürün, vergi profili, seçenek, fiyat oluşturulduktan sonra düzenlenemez/pasife alınamaz (yalnız müsaitlik ve hazırlık süresi); fiyat düzeltmesi OVERLAPPING_EFFECTIVE_PRICE yüzünden imkânsız. | Güncelleme/pasife alma uçları (row_version). |
| A-004 | Düşük | CatalogManagementStore.cs:199-219 | Fiyat ekleme ve current_price güncellemesi ayrı komutlar, transaction yok; araya hata girerse 500 ama fiyat yazılı, tekrar 409; current_price en çok 60 sn sonra arka plan servisince düzelir (CatalogPriceRecomputeHostedService.cs:30). | Tek transaction. |
| A-005 | Düşük | CatalogManagementStore.cs:92, 006:43 | Ürün açıklamasında uzunluk sınırı yok. | Üst sınır (2000). |
| A-006 | Düşük | CatalogManagementEndpoints.cs:248 (int.TryParse) vs DualScreenStore.cs:162 (NumberStyles.None) | limit ayrıştırması iki yerde farklı (" 5", "+5" yönetimde kabul). | Ortak ayrıştırıcı. |
| A-007 | Düşük | Program.cs:40, CatalogManagementEndpoints.cs:18, ApplicationPermissions.cs:109 | catalog.manage tanımı katalog dışında üç yerde dağınık; görünen ad İngilizce ("Manage catalog resources"); ekranda gösterilip gösterilmediği doğrulanamadı. | Sabit tek yerde, Türkçe ad. |
| A-008 | Düşük | CatalogManagementHttpTests.cs (NotFound araması 0; effective-price yalnız başarı satır 145; 401/403 yalnız POST /categories satır 54-80) | availability, prep-time, effective-price için 404; effective-price 400; availability/prep-time 409 CONCURRENCY_CONFLICT; bozuk/Base64 dışı imleç testleri yok. | Eksik yol testleri. |

Temiz: yetki filtresi 17/18 rotayı kapsıyor, 401/403 ayrımı var, terminal rotası kasiyer oturumu + terminalId eşleşmesi; hata gövdesi ham istisna sızdırmıyor (ApiErrorCatalog.cs:99-135, ApiErrorHandling.cs:54); imleç kaynağa bağlı/1024 sınırlı/COLLATE "C"; doğrulamalar ve parametreli SQL; ürün güncellemesi row_version ile (SKU yarışı testi satır 261); SameSite=Strict çerez.
Doğrulanamayan: hız sınırı değerleri (GlobalPartition gövdesi okunmadı); A-007 ekran gösterimi.

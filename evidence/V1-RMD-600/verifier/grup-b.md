# Doğrulayıcı - Grup B (SHA e00a61c0)

Sonuç: 16 kökün hiçbiri Kritik/Yüksek çıkmadı. En ciddi üçü Orta: K11 (yönetim ekranı ilk 100 kayıt), K10 (düzeltme/pasifleştirme ucu yok), K18 U-005 (garson ilk açılış katalog hatası). Çürüyenler: K19 B-008 (ofsetli tarih 500 değil 400), K20 G-005 (yönetim çerezi girişte terminal anahtarına bağlı), K22 V-012, K23 V-010, K26 D-005/D-006, K16 F-004 "çift kayıt". K08 gerçek ama Düşük (yalnız aynı anda iki yazmada, en geç 60 sn'de düzelir).

Çalıştırılanlar: Npgsql 9.0.2 deneyi (tmp/qa/V1-RMD-600/qa-bulgu-dogrulayici-B/npgprobe), 006/007/022/040/053/084/103 up ikişer kez + down zinciri geçici DB'de, performans kayıtları incelemesi. Geçici veritabanı alkaros_verify_b (test PG 55432) boş ama silinemedi (tehlikeli komut kancası) - Semih/oturum SQL dosyasıyla silecek.

| Kök / ham | Durum | Yeni önem | Tetiklenme | Özet |
|---|---|---|---|---|
| K08 B-006, V-003, O-005, T-002 | DOĞRULANDI (T-002 KISMEN) | Düşük (ajan Orta) | Koşullu | PostgresProductRepository.cs:165 UpdateAsync SET listesinde current_price; yeniden hesap/süpürme row_version artırmıyor; aynı anda availability/prep-time isteği eski fiyatı geri yazar; fiyat satırı varsa süpürme en geç 60 sn'de düzeltir. Tohum fiyatlı ürün senaryosu yalnız doğrudan SQL ile. |
| K08 S-001 | DOĞRULANDI | Düşük | Gizli | Host catalog.products'a ham SQL ile yazıyor (Store.cs:232, HostedService.cs:92,112). |
| K08 G-002 | KISMEN | Düşük | Bugün | POST /products CurrentPrice'ı fiyat geçmişi satırı olmadan yazar; bilinçli tasarım. Denetim izi K07. |
| K09 A-004, V-004, O-003 | DOĞRULANDI | Düşük | Koşullu | Store.cs:199 AddAsync ile :217 recompute ayrı komut, transaction yok. |
| K09 P-004 | DOĞRULANDI | Düşük | Gizli | İki UPDATE ayrı, idempotent. |
| K10 A-003, F-015, U-009, U-010 | DOĞRULANDI | Orta | Bugün | PUT/PATCH/DELETE yok; ad/SKU/KDV/kategori düzeltilemiyor; ekran "düzenleyebilir" diyor, "86" argosu. |
| K11 A-002, G-004 | DOĞRULANDI | Orta | Bugün (>100 kayıt) | catalogApi.ts:36 limit=100, nextCursor atılıyor; fiyat listesi product_id sırasında. |
| K12 A-001, B-010 | DOĞRULANDI | Düşük | Bugün (yalnız API) | 125.999 → 126.00 sessiz yuvarlama; UI step=0.01 engelliyor. |
| K16 F-004 | KISMEN | Düşük | Bugün | Çift gönderim mümkün ama çift kayıt oluşmaz (UNIQUE/EXCLUDE); yalnız 409 başarı mesajını ezer. |
| K16 F-005 | DOĞRULANDI | Düşük | Bugün | "empty" durumu hiç ayarlanmıyor; ilk kurulumda yanlış mesaj. |
| K16 F-006, U-011 | DOĞRULANDI | Düşük | Bugün | Her işlemden sonra tüm ekran yükleme mesajı. |
| K16 F-007 | DOĞRULANDI | Orta | Bugün (oturum dolunca) | catalogApi.ts:30-33 401'de onSessionExpired çağırmıyor. |
| K16 F-009 | KISMEN | Düşük | Bugün | Tohum fiyatlı ürün için "satılabilir değil" yanlış; "çift fiyat" kısmı yanlış. |
| K16 F-010, U-013 | DOĞRULANDI | Düşük | Bugün | toFixed(2) TRY biçimi; tarih yalnız gün. |
| K18 U-005 | DOĞRULANDI | Orta | Koşullu | Garson ilk katalog hatasında liste boş, yeniden dene yok. |
| K18 U-006 | DOĞRULANDI | Düşük | Bugün | Kasa filtre sonucu boşsa mesaj yok. |
| K18 U-007, F-011 | DOĞRULANDI | Orta | Bugün | cashier-app.js toLowerCase; "İ" ürünler aranınca bulunmuyor. |
| K18 U-016 | DOĞRULANDI | Düşük | Bugün | Garson 15 dk yenileme. |
| K18 F-013 | DOĞRULANDI | Düşük | Bugün | Üç istemcide üç boş mesaj. |
| K18 F-014 | KISMEN | Düşük | Bugün | "Açıklamasız" yanlış; ürün kararı. |
| K18 F-018 | DOĞRULANDI | Düşük | Koşullu | Tam SKU eşleşmesinde 120 ms sonra otomatik ekleme. |
| K19 B-008 | ÇÜRÜTÜLDÜ | Düşük | Yalnız API | 500 değil 400 VALIDATION_FAILED (ArgumentException eşlemesi); mesaj belirsiz. |
| K19 B-009, U-015 | DOĞRULANDI | Düşük | Bugün | datetime-local UTC/yerel kayması (3 saat). |
| K20 A-007, S-007 | KISMEN | Düşük | Gizli | İzin kodu üç yerde düz metin; ekrana İngilizce çıkmıyor. |
| K20 G-005 | ÇÜRÜTÜLDÜ | - | - | Çerez yalnız terminal anahtarı kontrolünden geçen girişte üretiliyor. |
| K20 G-006 | ÇÜRÜTÜLDÜ (bilinçli) | - | - | remaining_count V1-WTR-054 gereği. |
| K20 T-009 | DOĞRULANDI | Düşük | - | 401/403 testi yalnız POST /categories. |
| K21 B-012, S-005, S-006 | DOĞRULANDI | Düşük | Gizli | Transient önce kayıtlı; TryAddScoped etkisiz. |
| K22 P-002 | KISMEN | Düşük | Koşullu | 6000 üründe ölçüm; asıl maliyet satır başı LATERAL stok. |
| K22 P-003 | DOĞRULANDI | Düşük | Gizli | Süpürme Seq Scan. |
| K22 P-005 | DOĞRULANDI | Düşük | Gizli | 50 sayfa × 1000 sessiz kesinti. |
| K22 P-006, P-007, V-013 | DOĞRULANDI | Düşük | Gizli | İndeks gözlemleri; pg_stat bakılmadı. |
| K22 V-012 | ÇÜRÜTÜLDÜ | - | - | Çağıran yok. |
| K22 A-006 | DOĞRULANDI | Düşük | Bugün | Limit ayrıştırma farkı. |
| K22 A-005 | DOĞRULANDI | Düşük | Yalnız API | Açıklama uzunluk sınırı yok. |
| K23 V-008, V-009 | DOĞRULANDI | Düşük | Yeniden uygulamada | 022/084/103 ikinci uygulamada hata (idempotent değil). |
| K23 V-010 | ÇÜRÜTÜLDÜ | - | - | btree_gist down belgelenmiş tasarım. |
| K23 V-011, B-011 | DOĞRULANDI | Düşük | SelectOne+max>1 bugün | CHECK eksik; kategori döngüsü API'den kurulamaz. |
| K23 V-014, M-008 | DOĞRULANDI | Düşük | Gizli | ON DELETE CASCADE, silen çağıran yok. |
| K24 T-003..T-008, T-010 | DOĞRULANDI | Düşük | - | Test boşlukları (restore, prep-time sınırları, 404, imleç, TRY dışı süpürme, CONCURRENCY_CONFLICT). |
| K24 T-011, V-007 | DOĞRULANDI | Düşük | - | Fixture 084/097 uygulamıyor. |
| K24 T-012, K26 M-009 | DOĞRULANAMADI | - | - | Orders kapsamı. |
| K24 T-013, T-014 | DOĞRULANDI | Düşük | Gizli | UtcNow bağımlılığı, 100 eşiği. |
| K24 A-008 | KISMEN | Düşük | - | Bozuk imleç / 404 testi yok. |
| K24 F-017, U-018 | DOĞRULANDI | Düşük | - | /catalog E2E yok; axe kontrast kapalı. |
| K25 O-001, O-002 | KISMEN | Düşük | Gizli | Süpürme hatası yalnız log; genel DB kesintisini OutageWatchdog görür. |
| K25 O-006, O-007 | DOĞRULANDI | Düşük | Bugün | Genel hata mesajı; 60 sn sabit. |
| K25 O-008 | DOĞRULANDI | Düşük | - | EventId 5400 iki yerde (HostedService.cs:35, RelaySettingsEndpoints.cs:176). |
| K25 runbook | KISMEN | Düşük | - | product_prices saklama politikası yok. |
| K26 D-002, D-003 | Doküman sapması | - | - | tax_code 1/10/20 vs serbest vat_rate; X-Row-Version. |
| K26 D-005, D-006 | ÇÜRÜTÜLDÜ | - | - | Kural ileriye dönük. |
| K26 D-007 | DOĞRULANDI | Düşük | - | Gereksiz Npgsql referansı Host.Catalog testlerinde yok sayıldı (0 dosya). |
| K26 D-008 | Bilinen (V1-RMD-596) | - | - | - |
| K26 M-006 | DOĞRULANDI | Düşük | Bugün | "KDV dahil" ibaresi yok. |
| K27 U-008, U-012, U-014, U-017 | KISMEN/DOĞRULANDI | Düşük | Bugün | "Aktif" sabit, ham ISO tarih, İngilizce örnekler, .65rem etiket. |
| K27 U-010 | Yinelenen (K10) | - | - | - |

YENİ: (1) EventId 5400 çakışması (Düşük). (2) AddCatalogManagement() iki kez çağrılıyor, TryAdd ile etkisiz (Düşük).

Birleştirme: K08+K09 tek iş (fiyat önbelleğini UpdateAsync'ten çıkar + tek transaction). K10+K11 birlikte (düzenleme ucu eklenince liste tüm sayfaları çekmeli). K12 ölçek kontrolü UI step=0.01 ile uyumlu. K19 ToUniversalTime.

Doğrulanamayan: T-012, M-009 (Orders); üretimde JIT; pg_stat indeks kullanımı; AddHostedService yinelenen kayıt davranışı.

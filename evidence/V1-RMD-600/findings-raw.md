# V1-RMD-600 Catalog - birleştirilmiş bulgu listesi (SHA e00a61c0)

Ham bulgu: mevzuat 9, api 8, güvenlik 6, backend 13, veritabanı 14, sınır-mimari 7, doküman 8, ux 18 (+8 Fırsat), frontend 18, performans 7, operasyon 8, test-kalitesi 14 = 130 ham. Aşağıda kök nedene göre birleştirilmiş 27 kök (K01-K27). Her kökün sonunda ham kimlikler ve ajan raporları: evidence/V1-RMD-600/agents/<ajan>.md. Ajanların verdiği önem "ajan önemi"dir; doğrulayıcı "bugün tetiklenebilir mi" sorusuyla yeniden değerlendirir.

## Grup A (Yüksek adayları, tek tek doğrulanacak)
- K01 Fiyat kapatma/planlama yok: açık uçlu (effective_to boş) fiyat varken yeni fiyat 409 OVERLAPPING_EFFECTIVE_PRICE; eski fiyatı kapatan uç yok; UI effectiveTo göndermiyor. Ham: B-001, V-001, M-007, U-001(kısmen), A-003(kısmen). Ajan önemi Yüksek. Semih senaryosu.
- K02 Gelecek tarihli ilk fiyat current_price'ı NULL yapar, ürün bugün satıştan düşer (tohum fiyatlı ürün). Ham: B-002, U-001. Yüksek.
- K03 TRY dışı para birimiyle POST /prices current_price'ı yanlış para birimiyle yazar/NULL yapar (≤60 sn). Ham: B-003, V-002. Yüksek (B), Orta (V).
- K04 Profilsiz veya pasif KDV profilli ürün siparişe sessizce %0 KDV ile yazılır (5 sorgu: COALESCE(vat_rate,0)); Catalog ürün oluştururken profil doğrulamıyor; ürün formunda KDV seçimi yok. Ham: M-001, M-002, B-004, S-004, F-008(kısmen). Yüksek.
- K05 KDV oranı değiştirilemez: tax-profiles için yalnız GET/POST, geçerlilik tarihi yok, izinli oran kümesi/CHECK yok, tohum profil yok. Ham: M-003, M-005, B-005, V-005, V-006, T-001. Yüksek (M, B orta).
- K06 Yaş sınırı (is_age_restricted) yönetim API/modelinde yok; alan hep false; NFC/Kitchen kapısı fiilen kapalı. Ham: M-004, G-003, D-001. Yüksek (M), Orta (G, D).
- K07 Katalog yazmalarında (fiyat, KDV profili, ürün, current_price süpürmesi) denetim kaydı, olay (ProductCatalogChanged) ve log yok. Ham: G-001, S-002, O-004, D-004. Yüksek (G), Orta.
- K13 Modifikatör grubu ürüne bağlanamıyor: istemci product-modifier-assignments uç noktasını hiç çağırmıyor; atamalar yüklenmiyor. Ham: F-001. Yüksek.
- K15 Hata gösterimi: sunucu reddi modal açıkken banner'ı modalın arkasında bırakır; çakışma tespiti İngilizce regex ile Türkçe mesajı yakalayamaz. Ham: U-002, U-003, F-003. Yüksek (U), Orta (F).
- K17 Kasa (PosTerminal) her ürün dokunuşunda tüm kataloğu yeniden çeker; terminal-read 240/dk sınırı aşılır (429). Ham: P-001, F-012(kısmen). Yüksek (P).
- K14 UI'dan açılan ürünlerin kategorisi yok (hepsi "Diğer"). Ham: U-004, F-002. Yüksek (U), Orta (F).

## Grup B (Orta/Düşük)
- K08 current_price iki yazar (ürün güncelleme + süpürme), row_version artmıyor → lost update; Host ham SQL; POST /products CurrentPrice fiyat geçmişini atlıyor. Ham: B-006, V-003, S-001, O-005, T-002, G-002.
- K09 Fiyat ekleme ile current_price güncellemesi tek transaction değil. Ham: A-004, V-004, O-003, P-004.
- K10 Kategori/ürün/KDV/seçenek/fiyat düzenleme ve pasife alma uçları ve ekranı yok. Ham: A-003, U-009, F-015, U-010.
- K11 Yönetim ekranı yalnız ilk 100 kayıt (imleç izlenmiyor). Ham: A-002, G-004.
- K12 Fiyat/PriceDelta/VatRate 2 haneden fazlaysa sessiz yuvarlama, DTO farklı döner. Ham: A-001, B-010.
- K16 İstemci form/durum: F-004 çift gönderim, F-005 boş durum yanlış mesaj, F-006/U-011 her işlemden sonra tam yükleme flaşı, F-007 401 oturum bitişi, F-009 "satılabilir değil" metni, F-010/U-013 para biçimi.
- K18 Vanilla kasa/garson: U-005 garson katalog hatası, U-006 boş ızgara, U-007/F-011 Türkçe küçük harf, U-016 15 dk yenileme, F-013 üç farklı boş mesaj, F-014 yeni sipariş zorunluluğu, F-018 SKU otomatik ekleme.
- K19 Saat: B-008 ofsetli tarih 500, B-009/U-015 UTC/yerel kayması.
- K20 İzin/yetki: A-007/S-007 catalog.manage tanımı dağınık ve İngilizce ad; G-005 yönetim çerezi terminal anahtarına bağlı değil; G-006 stok adedi sızıntısı (bilinçli olabilir); T-009 ret testi dar.
- K21 DI yaşam süresi tutarsız (Transient/Scoped), çift kayıt. Ham: B-012, S-005, S-006.
- K22 Sorgu/indeks/performans: P-002 katalog sorgusu Seq Scan + top-N (soğuk 515 ms), P-003 süpürme 126k satır tarama, P-005 50 sayfa sessiz kesme, P-006/P-007/V-013 indeksler, V-012 collation/sıralama, A-006 limit ayrıştırma farkı, A-005 açıklama uzunluğu.
- K23 Göç hijyeni: V-008/V-009 idempotent değil, V-010 btree_gist down, V-011 CHECK eksikleri (üst kategori döngüsü, SelectOne max), B-011 aynı, V-014/M-008 ON DELETE CASCADE ve fiyat geçmişi saklama.
- K24 Test boşlukları: T-003, T-004, T-005, T-006, T-007, T-008, T-010, T-011 (fixture 022/097/084 uygulamıyor), T-012, T-013, T-014, A-008, F-017/U-018 (yönetim ekranı E2E yok).
- K25 Operasyon: O-001/O-002 süpürme izleme/bildirim yok, O-006 alan bazlı mesaj, O-007 60 sn sabit, O-008 EventId tablosu, runbook'ta katalog yedek/geri yükleme yok.
- K26 Doküman: D-002 tax_code 1/10/20 kavramı, D-003 X-Row-Version/Idempotency, D-005/D-006 kanıt klasörleri, D-007 gereksiz Npgsql, D-008 lisans envanteri, M-006 "KDV dahil" ibaresi yok, M-009 modifikatör KDV'si.
- K27 UX metin/görsel: U-008 fiyat listesinde geçerlilik tarihi yok ("Aktif" sabit), U-012 ham ISO damga, U-014 İngilizce örnekler/terim, U-017 küçük etiket, U-010 "86".

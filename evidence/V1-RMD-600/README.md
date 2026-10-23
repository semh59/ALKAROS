# V1-RMD-600 Catalog kalite denetimi (SHA e00a61c0)

## Semih için özet
- Senaryo "fiyat yarından geçerli" **karşılanmıyor**: açık uçlu fiyat kapatılamadığı için yeni fiyat 409 alıyor; ilk fiyat ileri tarihliyse ürün satıştan düşüyor (B002, B003).
- **Kritik:** ekrandan açılan her ürünün KDV profili yok ve siparişe sessizce %0 KDV yazılıyor; fatura taslağına geçiyor (B001).
- Yüksek: modifikatör grubu ürüne ekrandan bağlanamıyor (B004).
- Orta: ürün/fiyat/KDV düzeltme ve pasifleştirme ucu yok, liste ilk 100 kayıt, kategori alanı yok, katalog yazmaları iz bırakmıyor, kasa her dokunuşta kataloğu çekiyor.
- Çürüyen iddialar var (9); Kritik/Yüksek dördü kodda ve çalıştırılarak yeniden doğrulandı.
- Düzeltmeler ayrı görevler (V1-RMD-702...) olarak planlanır; Kritik/Yüksek önce.
- Semih'e sorular: S1 KDV'siz ürün için varsayılan oran? S2 izinli KDV oranları (%0 dahil mi, tarihli değişiklik)? S3 yaş sınırlı ürün grupları? S4 fiyat geçmişi saklama yılı? S5 seçenek (modifikatör) KDV oranı?

## Sayılar
130 ham bulgu, 27 kök. Doğrulayıcı sonrası: DOĞRULANDI 38, KISMEN 12, ÇÜRÜTÜLDÜ 9, DOĞRULANAMADI 3 (ham kimlik düzeyi, ayrıntı verifier/). Önem: Kritik 1, Yüksek 3, Orta 10, Düşük torba.

## Ajan çürütme oranı
Grup A: 11 kökte 0 çürütme, 3 kısmen, 2 önem değişimi (K04 yukarı, K06 aşağı). Grup B: 16 kökte 9 ham çürütme (ağırlıkla Düşük/Orta iddialar), 8 önem indirimi. En şişkin ajan önemi: performans (P-001 Yüksek → Orta), ux, test-kalitesi.

## Temiz alanlar
Fiyat penceresi ve çakışma dışlama testleri; row_version kontrolü; hazırlık süresi; catalog.manage kapısı; down zinciri 006-103 temiz; sunucu mesajları Türkçe; modifier sorgusunda N+1 yok.

## Doğrulanamayanlar
Orders kapsamındaki T-012/M-009; QNB'nin %0 KDV'li satırı kabul etme davranışı (dış kaynak); gerçek tarayıcıda görsel doğrulama; üretimde JIT; pg_stat indeks kullanımı.

## Fırsatlar (F001-F008)
Ürün ilk dokunuşta siparişi otomatik açsın; ortak para/tarih biçimi (tr-TR, Europe/Istanbul); ortak katalog boş mesajı; fiyat satırına geçerlilik aralığı; kasa katalog önbelleği/ETag; ürün formunda KDV ve kategori seçici; fiyat zamanlama ekranı; katalog değişiklik olayı (ProductCatalogChanged).

## Planlanan düzeltme görevleri (kök nedene göre)
702 KDV profili zorunluluğu + seçici + sipariş yolunda red (B001, B006, B009); 703 fiyat kapatma/planlama, ilk fiyat koruması, TRY, tek transaction (B002, B003, B005, B015); 704 yönetim ekranı düzenleme/pasifleştirme/sayfalama/hata (B010, B012, B013); 705 modifikatör atama ekranı (B004); 706 denetim izi (B008); 707 istemci düzeltmeleri kasa/garson (B011, B014); 708 Düşük torba (B016). B007 yaş sınırı S3 cevabını bekler.

## Dosyalar
baseline/, runs/, agents/ (12), findings-raw.md, verifier/grup-a.md, grup-b.md, findings.json, checks/verify-refs.{sh,log} (22 başvuru OK), closing/.
Geçici veritabanları test PG 55432'de: alkaros_verify_catalog_a, alkaros_verify_b (boş); silinmesi gerekir (tehlikeli komut kancası nedeniyle ayrı adım).
Denetim kod değiştirmedi.

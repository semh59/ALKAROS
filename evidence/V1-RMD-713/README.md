# V1-RMD-713 - Masa ekranlarının güvenilirliği ve sadeliği

Kapanan V1-RMD-601 bulguları: B009, B010, B011, B012, B017, B018, B019, B020 (istemci kısmı), B028.

## Değişiklikler
- Yoklama (8 sn) artık "yükleniyor" ekranına geçmez: açık pencere ve kaydedilmemiş kat planı taslağı korunur (B009). Yoklama hatası ekrandaki masaları silmez, "son bilinen durum" uyarısı gösterir; ilk yükleme hata verirse ekran sunucu dönünce kendi toparlanır (B019).
- Rezervasyon istasyonu eylem yapabilir; masa/bölge ekleme-düzenleme, kat planı kaydı ve sipariş köprüsü kapalı kalır (B010).
- Eylem hataları pencerenin içinde gösterilir; çakışmada pencere açık kalır ve güncel masa yüklenir. Hata alan eylemden sonra da liste yenilenir (B011).
- Kat planı çakışması doğru hata sınıfı ve koduyla (`CONCURRENT_MODIFICATION`) tanınır; "Taslağınız korundu" çıkar (B012).
- Garson: masa yaşı rozeti sunucunun gönderdiği `occupiedSince` alanını okur (B017); liste hatası boş liste olmaz, yeniden dene düğmesi ve son bilinen durum uyarısı vardır, liste 15 sn'de bir yoklanır, masa değiştirmede 409 sonrası masalar yenilenir (B018).
- Masa değiştirmede hedef yalnız boş masalar; birleştirmede yalnız boş veya dolu masalar. Gerekçe hazır seçenekle gelir, serbest metin yalnız "Diğer" için (B020).
- Okunabilirlik: açık turkuaz metin yerine koyu ton, 11 px altı yazı kalmadı, pasif eylemin nedeni görünür metin, "Satır sürümü", sürüm numaraları ve kimlikler ekrandan kalktı, son güncelleme saat olarak yazılır, kat planında sürüm numarası yok (B028).

## Test kanıtı (tümü exit 0)
tsc temiz; PosTerminal vitest 83 dosya / 787 test; garson `test_waiter_tables_reliability.py` 4/4. Günlükler `logs/`.

## Mutasyon (`mutate.mjs`, `mutations.log`)
24 mutantın 24'ü KILLED, kaynaklar geri yüklendi ve doğrulandı. W6 ilk koşuda SURVIVED (denk bir mutanttı: bölge oluşturma düğmesi iki izne birden bağlı); daha anlamlı mutantla (masa düzenleme) değiştirilip KILLED.

## Doğrulanamayan / açık kalan
- Garson testleri kaynak metni denetler (projenin mevcut yöntemi); tarayıcıda çalıştırılmadı.
- Kontrast gerçek oranı ölçülmedi; test yalnız açık turkuaz metin rengini ve 11 px altı yazıyı yasaklar.
- `tests/Clients/WaiterPwa/Frontend` içinde bu görevden bağımsız 6 eski test zaten kırık (`test_offline_authorized_void_comp`, `test_waiter_pwa_frontend`); değişiklikten önce de kırıktı.
- B020'nin sunucu tarafı (farklı bölge birleşimi ayrılamıyor, birleştirmek için önce "Masayı aç") V1-RMD-714'te.

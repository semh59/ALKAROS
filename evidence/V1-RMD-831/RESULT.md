# V1-RMD-831 sonuç

Kapatılan (T-20 gerçek geri alma): satıra ürün ve seçenek kimliği eklendi (`ProductId`, `ModifierIds`); satış ekranında kalem kaldırılınca bildirimde "Kalem kaldırıldı." ve "Geri al" çıkar. Geri al aynı ürünü aynı seçeneklerle kaldırılandaki adetle tek istekte yeniden ekler (ekle çağrısı adet alır; iki adımlı akış yarım kalabilirdi, ilk incelemede bulundu). Satır indirimi olan, katalogda artık olmayan ya da kimliği olmayan satırda sunulmaz; başka eylemde ve sipariş değişince kapanır.

Gizlilik: müşteri ekranı izin listesi testi (`SnapshotSerializesOnlyTheCustomerAllowlist`) kimlik sızmasını yasaklar. Yeni alanlar boşken JSON'a yazılmaz ve anlık görüntü yalnız kasa ucunda (`orders/active`) kimliklerle istenir (`includeLineIds`); müşteri ekranı yolları kimlik görmez (test: `ALineCarriesItsProductAndOptionIdsSoARemovedLineCanBeRebuilt`, `LineIdsReachOnlyASnapshotThatAskedForThem`).

## Doğrulama
- Sunucu: Host test projesi DualScreenStore + CustomerDisplay testleri 68/68. PosTerminal vitest 1071/1071 (güncel koşu), tsc 0.
- Mutasyon 7 mutant (indirim koşulu, katalog koşulu, adet, seçenekler, başka eylemde kapanma, sunum koşulu, kimliğin yalnız kasa ucunda istenmesi) hepsi öldürüldü (mutation-ts.txt, mutation-server.txt).

## İncelemeler (qa-frontend, qa-ux-tasarim)
Giderilen: ekle + adet-değiştir iki adımı tek isteğe indi (yarım kalan satır ve aynı ürünle birleşmede sessiz yanlış adet kalktı); başarısızlıkta sunucunun Türkçe iletisi görünür ve yarım satır oluşmaz (test).
Kabul edilen: ekleme isteği not, koltuk ve kurs taşımaz, `DisplayLine` de taşımadığı için geri kurulmayan özellik yok; geri alınan satır güncel katalog fiyatıyla gelir (kaldırıldığı andaki fiyat değil); seçenek adedi birden büyük satır tek adetle kurulur (sunucu AddOrderItemRequest seçenek adedi taşımaz).

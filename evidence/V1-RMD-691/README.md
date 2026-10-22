# V1-RMD-691 - Bildirim (push) sağlamlaştırma

## Ne değişti

- **Adres güvenliği (B005, B008):** kayıt yalnız `https`, varsayılan port, kullanıcı bilgisiz, noktalı alan adı kabul eder (IP, `localhost`, tek etiketli ad reddedilir). Gönderim istemcisi yalnız genel adreslere bağlanır, yönlendirme izlemez, bağlantıları 5 dakikada yeniler. Anahtarlar eğri üzerinde 65 baytlık nokta ve 16 baytlık gizli değer olarak doğrulanır.
- **Arka plan gönderim (B009, B010):** mutfak "hazır", bekleyen sipariş ve garson dağıtımı artık kuyruğa atar (`WebPushDispatchService`); istek, push servisini beklemez. Aygıtlara 8'e kadar paralel gönderilir. Hub hatası push'u engellemez (push önce kuyruğa girer).
- **Hata süzgeci ve budama (B011, B012, B013):** her hata türü aygıt başına yakalanır, diğer aygıtlar etkilenmez. Ağ hatası/5xx art arda 10 kez olursa abonelik silinir; başarı sayacı sıfırlar.
- **Sınırlar (B029, B059):** kullanıcı başına 10 abonelik (en eski silinir); başka kullanıcının başka terminaldeki adresi devralınamaz (409); pasif kullanıcıya bildirim gitmez.
- **Bildirim içeriği (B060–B063 kısmen):** hazır bildirimi masa numarasını söyler, etiket kalem başınadır; istemcide aynı etiket kullanılır. İzin reddedilmişse ne yapılacağı söylenir; uygulama açılışında abonelik yeniden kaydedilir; başarısız çıkışta bildirim geri açılır.
- **VAPID özel anahtarı (B098):** göç 199 ile `private_key_envelope`; yeni anahtar yalnız şifreli yazılır, eski düz anahtar ilk okumada şifrelenip düz sütun temizlenir.

## Kanıt

- `logs/webpush-tests.log`, `logs/related-test-projects.log`, `logs/architecture-and-manifest-tests.log`, `logs/host-tests-full.log`: ilgili projeler yeşil (WebPush 95/95, Mutfak 69, Bekleyen sipariş 12, Composition 22, HelpRequests 22, TableDraft 93, ModuleBoundaries 10, ApiConventions 5, Manifest 16, tam Host.Tests 203/203).
- `mutate.mjs` / `mutations.log`: 46 mutant, hepsi KILLED, kaynak her seferinde geri yüklendi (`restored=true`). İlk geçişte `if (false)`/kullanılmayan değişken yüzünden 5 mutant derlenmedi (BUILD-ERROR) ve M37 (DI'daki istemci) sağ kaldı; mutant yeniden yazıldı, M37 için `TheRegisteredSenderOnlyConnectsToPublicAddresses` testi eklendi, ikinci geçişte hepsi KILLED.
- İstemci mutantları (C1–C7) yalnız metin tabanlı Python testleriyle öldürülür.

## Yapılmayanlar / doğrulanamadı

- Anahtar döndürme (rotation) yok: VAPID anahtarı döndürülürse aygıtların yeniden abone olması gerekir; bu görevde uygulanmadı.
- B060 (yeniden bağlanınca kaçırılan "hazır" kalemleri tamamlama) yapılmadı; yeni uç nokta gerektirir.
- `sw.js` `pushsubscriptionchange` ve `notificationclick` içindeki `url` yönlendirmesi değişmedi; abonelik kaybı uygulama bir sonraki açılışta onarılır (arka planda anında değil).
- Mutfak "hazır" bildiriminde masa numarası sorgusu (`NotifyWaitersItemIsReadyAsync`) otomatik testle doğrulanamadı; mesaj biçimi testli, sorgu satırı değil.
- Tarayıcıda gerçek push (FCM/Mozilla/Apple) ve iOS akışı denenmedi; istemci davranışı yalnız metin testiyle korunuyor. Mevcut 6 Python testi (3'ü bu görevden önce de kırık, 3'ü Windows kodlaması) bu görevle ilgisiz kalmaya devam ediyor.
- Bağlantı anındaki genel adres denetimi `localhost` ile doğrulandı; gerçek bir DNS yeniden eşleme saldırısı denenmedi.

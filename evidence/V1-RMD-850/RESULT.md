# V1-RMD-850 sonuç

## Kusur ve düzeltme
Çevrimdışı iptal/ikram onaylanınca kalem ekranda kalır ve sayfa aynı kalem için aynı işlem anahtarını yeniden verir; `queueOfflineAuthorizedAction` aynı anahtarı kuyruğa ikinci kez ekliyor ve yerel yetki bütçesini ikinci kez harcıyordu (kırmızı: `["k-void","k-comp","k-void"]`). Kuyrukta aynı anahtar varsa artık ne kayıt eklenir ne sayaç artar (`offline-queue.js`, 3 satır).

## Davranış testleri
`waiter-app.js` Node'da içe aktarılamadığından kaynak-metni testiyle korunan iki kablolama `waiter-app.test.js` içinde gerçek davranış testine çevrildi (kaynak-metni testi silindi): girişte sunucunun verdiği bütçe saklanır; `online`, uygulamanın yeniden görünür olması ve canlı bağlantının (SignalR) açılması kuyruktaki çevrimdışı işlemleri tek istekle uzlaştırır.

## Kanıt
- Kırmızı `red.log`: 4 Python testi düşüyor (3 kayıt, bütçe sayacı). Yeşil `green.log`: WaiterPwa Frontend 34/34; `green-static.log` ve `full-static.log`: StaticApps 33 dosya, 370 test geçti.
- Mutasyon `mutations.log` (cp+cmp ile geri alındı): tekrar denetimi kalkarsa 4 Python testi, girişte bütçe saklanmazsa 1, `online` tetikleyicisi kalkarsa 2, görünür olma tetikleyicisi kalkarsa 1, arka plandayken uzlaştırma yapılırsa 1, canlı bağlantı tetikleyicisi kalkarsa 1 test düşer.

## Açık not
- Elle deneme (tarayıcıyı çevrimdışı yapıp aynı kalemi iki kez iptal etmek) bu ortamda yapılmadı; davranış harness ve jsdom testleriyle doğrulandı.
- İnceleme bulgusu (V1-RMD-852'ye devredildi): aynı kalemin ikinci çevrimdışı onayında hak (`maxCount`) dolmuşsa `void-comp.js` `authorizeOffline` yetki denetimini kuyruk tekrar denetiminden önce yapar; garson bekleyen kendi kaydı için yanlış "hakkınız doldu" uyarısı görür (çift kayıt oluşmaz). `void-comp.js` bu görevin yüzeyi dışında.
- Test sızıntısı: `waiter-app.test.js` `visibilityState` değerini her testten sonra siler. Önceki uygulama örneklerinin `window` dinleyicileri `vi.resetModules` ile kaldırılmaz; testler kuyruğu boş bıraktığından tek istekle geçer (mevcut dosyadaki diğer testlerde de aynı kalıp).

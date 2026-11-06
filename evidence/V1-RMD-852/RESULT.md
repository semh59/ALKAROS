# V1-RMD-852 sonuç

V1-RMD-850 incelemesinde bulunan kusur: aynı kalemin çevrimdışı iptal/ikram onayı kuyrukta beklerken ikinci kez onaylanırsa ve yetki hakkı (`maxCount`) o ilk onayla dolmuşsa, `authorizeOffline` yetki denetimini önce yaptığı için garson "Çevrimdışı işlem hakkınız doldu" uyarısı görüyordu (kayıt zaten kuyruktaydı, çift kayıt oluşmuyordu).

## Değişiklik
- `offline-queue.js`: `isOfflineActionQueued(key)` eklendi; V1-RMD-850'nin `queueOfflineAuthorizedAction` içindeki sessiz tekrar koruması buraya taşındı (tek yerde).
- `void-comp.js` `authorizeOffline`: kuyrukta aynı anahtar varsa yetki denetimi ve bütçe harcaması atlanır, sayfa kapanır ve "Bu işlem zaten çevrimdışı kuyrukta; bağlanınca uzlaştırılacak." gösterilir.
- Test (`test_offline_authorized_void_comp.py`): `maxCount: 1` bütçesinde ikinci onay; harness hatasında alt sürecin stderr'i artık test hatasında görünür.

## Kanıt
- Kırmızı `red.log`: yeni test düşer (uyarı "hakkınız doldu"). Yeşil `green.log`: WaiterPwa Frontend 35/35; `full-static.log`: StaticApps 370/370.
- Mutasyon `mutations.log` (cp+cmp ile geri alındı): kuyruk denetimi kaldırılırsa 5 test, yetki denetiminden sonraya alınırsa 1 test düşer.

## Açık not
- Elle deneme (çevrimdışı, aynı kalemi iki kez iptal) bu ortamda yapılmadı; davranış harness ile doğrulandı.

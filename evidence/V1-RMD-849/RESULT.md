# V1-RMD-849 sonuç

`test_offline_authorized_void_comp.py` üç testi kırmızıydı. Kök nedenler: (1) Node çıktısı Windows kod sayfasında çözülüyordu, Türkçe mesaj bozuluyordu (iki test); (2) üçüncü test kaynak metnini arıyordu ve V1-RMD-671 sonrası kod (iptalin kalemin brüt tutarıyla kuyruğa alınması) ile uyuşmuyordu.

## Değişiklik
- Alt süreç çıktısı UTF-8 çözülür, 60 sn zaman aşımı vardır (bir döngü hatası testi sonsuza dek bekletmez).
- Bayat kaynak-metni testi yerine Node'da gerçek `confirmVoidSent` / `confirmComp` / `reconcileOfflineActions` çalışır: iptal ve ikram kalem tutarı + kendi anahtarıyla kuyruğa girer, bütçeyi aşan ikram girmez, bağlanınca tek istek `/offline-reconciliation` gider, kuyruk boşalır ve sunucunun Türkçe özeti garsona gösterilir.
- Giriş ve bağlanınca uzlaştırma kablolaması (`rememberOfflineBudget` girişte, `reconcileOfflineActions` 3 yerde) şimdilik kaynak-metni testiyle korunur; `waiter-app.js` Node'da içe aktarılamadığından davranış testi V1-RMD-850'de.

## Kanıt
- Kırmızı `red.log`: 3 test düşüyor. Yeşil `green.log`: 12/12; `full.log` (WaiterPwa Frontend): 34/34.
- Mutasyon `mutations.log` (cp+cmp ile geri alındı): iptal tutarı 0, bütçe reddi atlandı, anahtar taşınmıyor, uzlaştırma sonrası kuyruk boşalmıyor (zaman aşımı hatası), sunucu özeti gösterilmiyor, girişte bütçe saklanmıyor, ağ hatası kuyruğu koruyor: yedisinde de test düşer.

## Açık notlar (V1-RMD-850'ye devredildi)
- İnceleme sırasında gerçek üretim kusuru bulundu: aynı kalem için çevrimdışı iptal/ikram ikinci kez onaylanırsa (sayfa aynı anahtarı yeniden kullanır) aynı `idempotencyKey` kuyruğa iki kez girer ve yerel bütçe iki kez harcanır (harness çıktısı: `["k-void","k-comp","k-void"]`). Düzeltme üretim kodunda; bu görevin yüzeyi dışındadır.
- Garson PWA üretim koduna bu görevde dokunulmadı.

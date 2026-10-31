# V1-RMD-777 - Sonuç

Karar (Semih): sayaçlar gerçek olsun; sipariş adedi = porsiyon (stok miktarı reçeteye bölünmez).

## Ne değişti
- Eşleme: `IDailyMenuItemLocator` bugünkü iş gününün `Open` menüsünde ürünü içeren aktif kalemi bulur (menü+ürün benzersiz, belirsizlik yok; menüde yoksa sayılmaz).
- Yazım: arbiter, sipariş kalemi başına yalnız ilk stok rezervasyonuna `daily_menu_item_id` + `portion_count` yazar (iki stok eşlemeli ürün çift sayılmaz). Üretimde rezervasyon üreten tek yol bu arbiter (QR kabulü, çevrimiçi sipariş/Trendyol Go).
- Olay yolu (outbox, aynı işlemde): rezervasyon oluşumu, tüketim (guard), repository terminal geçişleri (bırakma/fire/tüketim), üretim çıktısı -> Menu `DailyMenuCounterEventConsumer` -> mevcut projector metotları. Terminal olay önce gelirse Reserved önce uygulanır (idempotent).
- Rebuild/drift/rapor `portion_count` toplar; `available_portions` 0'a kırpılır (üretimsiz kalem CHECK ile ölü mektuba düşmesin).
- Rebuild/drift için `/daily-menus/{id}/counters/rebuild|drift` uç noktaları (ölü kod kalmasın).

## Sınırlar ve açık riskler
- Eski veri: geçmiş rezervasyonlarda anahtar yok, doldurma yapılmadı; yalnız yeni rezervasyonlar sayılır, rebuild yalnız anahtarlıları sayar.
- KANAL KAPSAMI (karar gerekir): garson, kasiyer ve NFC siparişleri rezervasyon üretmeden doğrudan stok düşer; bu kanallar sayaca GİRMEZ. Seçenekler: (A) yalnız QR + çevrimiçi sayılır (şimdiki durum); (B) tüm kanallar sayılır -> ayrı görev, sipariş tüketiminden ikinci bir kaynak gerekir.
- Üretimsiz kalemde `available`=0 ve `isOutOfStock`=true görünür (üretim kaydı yoksa "tükendi").
- Üretim partisi birimi serbest metin; `prepared` çıktı adedini birimine bakmadan toplar (önceden de böyleydi).
- Rebuild, eşzamanlı Apply'a karşı kilitsiz (739 ayrı bulgusu); operatör aracı.
- Menü "Open" iken kapanışta gelen terminal geçişler sayılmaya devam eder; yeni rezervasyon yalnız Open menüde sayılır.
- İş günü sınırı: `BusinessDateProvider` gün değişimini takvim gece yarısı yapar; gece yarısından sonra gelen sipariş, yeni tarihte Open menü yoksa sayılmaz (menü tarih bazlı, mevcut kural).
- Anahtarı olup `portion_count` olmayan satır yalnız dışarıdan yazılırsa olur; kodda üretilmez.
- Eski kullanılmayan tek satırlı arbiter yolu (`IPortionReservationArbitrator`) üretimde çağrılmıyor; olay üretmez.

## Kanıt
- Testler: `Modules_*`, `Host_*`, `arch-*`, `menu-projection-2`, `production-1` klasörleri (exit.txt = 0). 
- Mutasyon (hepsi kırmızı, kaynak cmp ile geri): mut1 anahtar yazımı, mut2 tüketici kapalı, mut3 stok miktarı toplama, mut4 repository olayı, mut5 guard olayı, mut6 üretim olayı, mut7 kırpma.
- plan-audit.txt, consistency-audit.txt, task-scope çıktıları bu klasörde.

## İnceleme sonrası notlar
- qa-backend: iş günü sınırı, anahtarsız `portion_count`, kullanılmayan tek satırlı arbiter ve allowlist gerekçesi ele alındı (yukarıda risk olarak yazıldı, gerekçe metni düzeltildi).
- qa-test-kalitesi: mutfak başladıktan sonra iptal (Waste, işlem içi geçiş) testi eklendi; Production fixture'ında rastgele tarih kaldırıldı. Kalan: QR süre dolumu ve Yemeksepeti iptalinde sayaç düşüşü Host seviyesinde testsiz (aynı repository/arbiter yolunu kullanır, modül testi kapsar); `MenuModule` DI kaydı testsiz (Host Composition testleri çalışıyor ama tüketici kaydını doğrulamıyor); Trendyol Go aynı `YemeksepetiOrderIntakeService` üzerinden geçer, ayrı sayaç testi yok.

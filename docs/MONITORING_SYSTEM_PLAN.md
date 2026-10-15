# ALKAROS Hata ve Performans İzleme Sistemi — Plan

Durum: plan kayıtları hazır (V1-RMD-533…557), kod yazılmadı. Tarih: 2026-10-15.

## 1. Amaç

Restoranda ortaya çıkan her hatayı ve performans kaybını — en ufağını — görmek; arayüzde basılan her düğmenin ve hareketin kaydını tutmak; müdürün bunu tek ekranda görmesi, kritik durumda Telegram ile haber alması.
LojiNext'teki hata yakalama sistemi referans alındı; ALKAROS'a uygun (tek restoran, yerel ağ, Docker) ve onun zayıflıkları (süreç belleğinde tekilleştirme, kuyruk dolunca olay kaybı, tek kanal) olmadan kurulacak.

## 2. Kararlar

- Hata olayları mevcut uyarı çatısını (`AlertFoundation`) besler; ikinci bir uyarı sistemi kurulmaz.
- Ekran, yönetimdeki mevcut **Sistem** bölümünü genişletir.
- Bildirim: yönetim ekranı + Telegram. Telegram'a yalnız **sabit künye** gider (kategori, sayı, saat); hata metni, müşteri, tutar ve ürün adı gitmez.
- Kapsam: tüm alanlar ilk dalgada (Z-skor şiddeti, zaman çizelgesi, derin veritabanı izleme, gerileme tespiti dahil).
- Gizlilik önce gelir: hangi düğmeye basıldığı kaydedilir, düğmenin yazısı veya girilen değer kaydedilmez.
- Oturum videosu kaydı yapılmaz.

## 3. Terimler

| Terim | Anlamı |
|---|---|
| p50 / p95 / p99 | Süre ölçüsünde yüzde kaçının bu sürenin altında kaldığı. p95: 20 işlemden 19'u bundan hızlı. |
| İzleme kimliği (trace id) | Bir eyleme verilen tek numara; düğmeden veritabanına her adım aynı numarayı taşır. |
| Aralık (span) | Eylemin içindeki bir adım, ör. "veritabanı sorgusu 240 ms". |
| Telemetri | Sistemin kendi hakkında topladığı ölçüm ve olay verisi. |
| Kanarya (synthetic probe) | Gerçek kullanıcı yerine sistemin kendi kendine yaptığı deneme. |
| Taban çizgisi (baseline) | Bir eylemin normalde ne kadar sürdüğü; sapma buna göre ölçülür. |
| INP | Düğmeye dokunma ile ekranın yanıt vermesi arasındaki süre. |
| Uzun görev | Ekranı 50 ms'den uzun donduran işlem. |
| Parmak izi | Aynı hatayı tanıyan imza; sayı ve kimlikler çıkarılarak üretilir, tekrarlar tek satırda sayılır. |
| Oran sınırı | Aynı kaynaktan gelen olay sayısını sınırlamak (sistemi boğmasın diye). |
| Devre kesici | Hedef çalışmıyorsa tekrar tekrar denemeyi bırakıp yedek yola geçen koruma. |

## 4. Uygulama sırası

| Sıra | Görev | Başlık | Bağımlı olduğu |
|---|---|---|---|
| 1 | V1-RMD-550 | İzleme sistemi karar kaydı | - |
| 2 | V1-RMD-551 | İzleme bağlama görevi (ortak dosyalar) | 550 |
| 3 | V1-RMD-549 | İzleme verisi gizliliği ve saklama | 550, 551 |
| 4 | V1-RMD-533 | Hata olayı çekirdeği | 549, 551 |
| 5 | V1-RMD-552 | Çöküş kanalı ve süreç dışı bekçi | 533 |
| 6 | V1-RMD-534 | Sunucu dedektörleri | 533 |
| 7 | V1-RMD-554 | Güvenlik, iş kuralı ve entegrasyon dedektörleri | 533 |
| 8 | V1-RMD-555 | Sistem sağlığı görüntüsünü yazma | 533 |
| 9 | V1-RMD-538 | Bildirim kuyruğu ve Telegram kanalı (iç) | 533, 549 |
| 10 | V1-RMD-542 | İzleme kimliği zinciri | 533 |
| 11 | V1-RMD-541 | Arayüz etkileşim izleme | 533, 549, 542 |
| 12 | V1-RMD-536 | Akıllı şiddet ve izleyicinin izlenmesi | 533 |
| 13 | V1-RMD-535 | Alan dedektörleri | 533, 555 |
| 14 | V1-RMD-537 | Tarayıcı hata raporlama | 533, 549 |
| 15 | V1-RMD-553 | Vanilla kasa ve garson uygulamalarında raporlama | 537, 541 |
| 16 | V1-RMD-543 | Performans ölçümleri ve yüzdelikler | 533, 542 |
| 17 | V1-RMD-544 | Performans bütçeleri ve gerileme alarmı | 543 |
| 18 | V1-RMD-545 | Sentetik sağlık sondası (kanarya) | 533 |
| 19 | V1-RMD-546 | Veritabanı derin izleme | 533 |
| 20 | V1-RMD-547 | Kaynak izleme | 533 |
| 21 | V1-RMD-539 | Yönetim ekranı: sistem sağlığı ve hatalar | 533, 536, 538 |
| 22 | V1-RMD-548 | Kullanıcı yolculuğu ekranı | 541, 542, 543 |
| 23 | V1-RMD-556 | İzleme yükü ve fırtına ölçümü | 534, 541, 543 |
| 24 | V1-RMD-557 | Telegram gerçek doğrulama (dış hesap) | 538 |
| 25 | V1-RMD-540 | Hata yakalama sistemi uçtan uca kanıt | 534, 535, 537, 538, 539, 541, 545, 546, 547, 548, 552, 553, 554, 556 |

## 5. Görev ayrıntıları

### V1-RMD-550 — İzleme sistemi karar kaydı

Yeni hata/performans izleme sisteminin mevcut uyarı sistemi ve planlı V15-OBS-002, V15-OBS-003, V15-NOT-001 ile ilişkisi, Telegram içerik politikası ve izleme verisi için KVKK değerlendirmesi karar olarak kayda geçer.

Kabul koşulları:
1. Kayıt, her karar için gerekçe ve dayanak içerir; örtüşme tablosu üç V15 görevini ve V1-ALT-001'i kapsar.

### V1-RMD-551 — İzleme bağlama görevi (ortak dosyalar)

İzleme görevlerinin dokunması gereken ortak dosyalar tek sahipli bu görevde bağlanır; diğer görevler yalnız kendi klasörlerine yazar.

Kabul koşulları:
1. Ayrı havuz üst sınırı 4 bağlantıdır; izleme yazımları ana havuzu sayaçla ölçülen şekilde tüketmez.
2. Tutarlılık denetimi ve mimari testler sıfır hata.

### V1-RMD-549 — İzleme verisi gizliliği ve saklama

İzleme verisi kişisel veri, ödeme verisi veya girilen metin taşımaz; her tablonun saklama süresi tanımlıdır; süresi dolan veri silinir; ham tıklama verisi yedeğe girmez.

Kabul koşulları:
1. Sızıntı testi: 12 kişisel veri kalıbı örneğinin hiçbiri depoya yazılmaz.
2. Saklama işi 30 günü aşmış satırların %100'ünü siler, süresi dolmamışlara dokunmaz.
3. Ham tablolar yedek dosyasında yer almaz (yedek içeriği testle kanıtlanır).

### V1-RMD-533 — Hata olayı çekirdeği

Her hata katman ve şiddetle tek bir hata olayı olarak kaydedilir; parmak izi mevcut uyarı çatısının tekilleştirme anahtarı olur; yazım toplu ve sınırlıdır.

Kabul koşulları:
1. Fırtına testi: aynı hatanın saniyede 500 oluşu en çok 1 yazım/sn/parmak izi ve ana havuzdan 0 bağlantı tüketir.
2. Parmak izi: kimlik ve sayı farkı olan 20 örnek tek satıra düşer.
3. Kuyruk dolunca düşen olay sayacı artar ve ekrandan okunur.
4. Gerçek Postgres testi ve mutasyon kanıtı.

### V1-RMD-552 — Çöküş kanalı ve süreç dışı bekçi

Veritabanı veya API süreci çökse de kritik durum yerel dosya tamponuna yazılır, doğrudan bildirim kanalına gider ve süreç dışı bir bekçi "sistem ayakta" nabzını izler.

Kabul koşulları:
1. Veritabanı durdurulunca 60 sn içinde bildirim kuyruğuna veya doğrudan kanala kritik olay düşer.
2. API konteyneri durdurulunca bekçi 90 sn içinde alarm üretir.
3. Tampon üst sınırı 5 MB; dolunca en eski kayıt atılır ve sayılır.

### V1-RMD-534 — Sunucu dedektörleri

Yakalanmamış istisna, yavaş istek, yavaş sorgu, bağlantı havuzu, kilit/kilitlenme ve kaçan arka plan işi hata olayı olarak yakalanır.

Kabul koşulları:
1. 500 durumlu bir istek tam 1 KRİTİK olay; 404 ve 409 sıfır olay üretir.
2. Yapay 10 sn kilit bekleme 55P03 olayı üretir.
3. İzleme yazımları yavaş-sorgu olayı üretmez (öz kirlenme testi).

### V1-RMD-554 — Güvenlik, iş kuralı ve entegrasyon dedektörleri

Yetki reddi yağmuru, kaba kuvvet giriş denemesi, iş kuralı ihlali, outbox kalıcı başarısız mesajlar ve QR tüneli kopması hata olayı olur.

Kabul koşulları:
1. 60 sn'de 10 başarısız giriş 1 olay üretir.
2. Outbox'ta kalıcı başarısız mesaj 1 dakika içinde olay olur.

### V1-RMD-555 — Sistem sağlığı görüntüsünü yazma

Mevcut sistem sağlığı görüntüsünü yazan kod bağlanır; yönetim ekranındaki boş "sağlık" bölümü gerçek veriyle dolar.

Kabul koşulları:
1. Görüntü 60 sn aralıkla yazılır; yedek yaşı ve disk doluluğu ekranda sıfırdan farklı gerçek değer gösterir.

### V1-RMD-538 — Bildirim kuyruğu ve Telegram kanalı (iç)

Kritik hata anında, diğerleri 5 dakikalık özetle bildirim kanalına gider; Telegram, V15-NOT-001 kanal sözleşmesinin bir kanalıdır; yalnız sabit künye gönderilir.

Kabul koşulları:
1. Kritik olay kuyruğa 5 sn içinde girer; gönderim ilk denemede 60 sn içinde.
2. Bot anahtarı günlüklerde ve hata metninde 0 kez geçer (test).
3. 100 birikmiş bildirim 1 özet mesajı olur.

### V1-RMD-542 — İzleme kimliği zinciri

Bir düğmeye basıldığında başlayan izleme kimliği isteğe, veritabanı komutuna, olaylara ve denetim kaydına taşınır; mevcut `X-Correlation-Id` ile hizalanır.

Kabul koşulları:
1. Bir düğme eyleminin kimliği istekte, olayda ve denetim kaydında aynıdır (uçtan uca test).
2. Alt aralık sayısı eylem başına en çok 50.

### V1-RMD-541 — Arayüz etkileşim izleme

Kasa, garson ve mutfak ekranlarında basılan her düğme, sayfa değişimi ve form gönderimi etkileşim olarak kaydedilir: ne, ne zaman, hangi rol/terminal, ne kadar sürdü, sonucu ne oldu.

Kabul koşulları:
1. PosTerminal'deki tüm düğmeler eşleme tablosunda anahtara sahiptir (statik tarama testi).
2. Tıklama ek yükü gerçek kasa cihazında p95 5 ms altında.
3. Telemetri kuyruğu dolunca sipariş kuyruğu yazımı etkilenmez.

### V1-RMD-536 — Akıllı şiddet ve izleyicinin izlenmesi

Bir hata normalden çok sıklaşırsa şiddeti otomatik yükselir (saat dilimi ve hacim farkındalığıyla); dedektörler çalışmazsa bu da bir alarmdır.

Kabul koşulları:
1. Açılış saatinde (düşük taban) tek hata yükseltme üretmez.
2. Taban 10 kat aşılınca yükseltme 5 dakika içinde olur.
3. Dedektör durdurulunca 2 nabız süresi içinde `detector_degraded` olayı.

### V1-RMD-535 — Alan dedektörleri

Yazıcı kopması, Bilinmeyen ödeme sonucu, mutfağa iletilmeyen sipariş, Yemeksepeti/QNB/Trendyol Go entegrasyon hataları, yedek ve disk durumu hata olayı olarak yakalanır.

Kabul koşulları:
1. Her alan için en az 1 enjekte edilen hata olay üretir ve düzelince 1 dakika içinde çözülür.
2. Bilinmeyen ödeme 5 dakikayı aşınca KRİTİK olur.

### V1-RMD-537 — Tarayıcı hata raporlama

PosTerminal ekranındaki yakalanmamış hatalar ve ağ hataları sunucuya raporlanır (vanilla kasa ve garson V1-RMD-553'te).

Kabul koşulları:
1. Oturumsuz çağrı 401; oran sınırı aşımı gerçek hata kaydını engellemez (ayrı kota).
2. Aynı hata 50 kez raporlanınca sunucuya en çok 3 ulaşır.
3. Metin sızıntı testi 0 kalıp.

### V1-RMD-553 — Vanilla kasa ve garson uygulamalarında raporlama

Düz JavaScript ile yazılmış kasa ve garson uygulamaları da hata ve etkileşim raporlar; ödeme ekranları dahildir.

Kabul koşulları:
1. Ödeme ekranındaki 5 düğme için olay kaydı (StaticApps testi).
2. Garson çevrimdışı sipariş kuyruğu (localStorage) telemetri nedeniyle kota hatası vermez.

### V1-RMD-543 — Performans ölçümleri ve yüzdelikler

Her eylem ve uç nokta için istek sayısı, hata oranı ve süre dağılımı (histogram) dakikalık özetlerle saklanır; yüzdelikler (p50, p95, p99) histogramdan hesaplanır.

Kabul koşulları:
1. Bilinen dağılımlı 10.000 örnekle p95 hatası en çok %5.
2. Yazım hacmi: dakikada en çok 600 özet satırı.

### V1-RMD-544 — Performans bütçeleri ve gerileme alarmı

Her kritik eylemin süre bütçesi vardır; bütçe aşılınca veya normale göre yavaşlayınca alarm üretilir.

Kabul koşulları:
1. Bütçe aşımı 3 ardışık dakikada 1 alarm üretir (tekrar bastırma).
2. Yeni sürüm sonrası %50 yavaşlama gerileme olayı olur.

### V1-RMD-545 — Sentetik sağlık sondası (kanarya)

Sistem her dakika kendi üzerinde salt okuyan veya işlemi geri alan bir akışı dener ve süresini ölçer.

Kabul koşulları:
1. Bir günde kalıcı sentetik kayıt sayısı 0.
2. Sondanın kendisinin yükü dakikada en çok 5 sorgu.

### V1-RMD-546 — Veritabanı derin izleme

En yavaş sorgular, kilitler, bağlantı doluluğu ve tablo şişmesi veritabanının kendi istatistiklerinden izlenir.

Kabul koşulları:
1. Eklenti yokken sistem hata vermeden "kapalı" gösterir.
2. Yapay yavaş sorgu 1 dakika içinde listede görünür.

### V1-RMD-547 — Kaynak izleme

Konteyner kaynakları (işlemci, bellek, disk), gerçek zamanlı bağlantı kopmaları ve garson çevrimdışı kuyruk uzunluğu izlenir.

Kabul koşulları:
1. Disk %90 eşiği olay üretir.
2. Bağlantı kopma sayacı gerçek bir kopmada artar.

### V1-RMD-539 — Yönetim ekranı: sistem sağlığı ve hatalar

Müdür, mevcut Sistem bölümünde canlı hata listesini görür; süzer, ayrıntıya ve tekrar sayısına bakar, hatayı onaylar veya çözüldü işaretler.

Kabul koşulları:
1. 10.000 olayda liste ilk sayfa 300 ms altında.
2. Ekran metinlerinde İngilizce sızıntı taraması 0.

### V1-RMD-548 — Kullanıcı yolculuğu ekranı

Müdür bir eylemin zaman çizelgesini görür: düğme, istek, veritabanı, olay; her adımın süresi ve en yavaş eylemler.

Kabul koşulları:
1. Seçilen eylemin tüm adımları tek izleme kimliğiyle sıralı görünür.
2. Yetkisiz rol 403 alır.

### V1-RMD-556 — İzleme yükü ve fırtına ölçümü

İzleme sisteminin kasa terminaline ve veritabanına yükü ölçülür; olay fırtınasında davranışı kanıtlanır.

Kabul koşulları:
1. İzleme açıkken sipariş onayı p95 süresi kapalıya göre en çok %10 yavaş.
2. Saniyede 1000 olay fırtınasında ana havuz bağlantı kullanımı değişmez.

### V1-RMD-557 — Telegram gerçek doğrulama (dış hesap)

Gerçek Telegram bot hesabıyla kritik olayın ve özetin teslimi doğrulanır; dış hesap gelene kadar bekler.

Kabul koşulları:
1. Gerçek hesaptan alınan 3 mesajın kanıtı (künye biçimi).
2. Bot anahtarı kanıt dosyalarında yer almaz.

### V1-RMD-540 — Hata yakalama sistemi uçtan uca kanıt

Docker yığınında enjekte edilen gerçek hatalar olay olarak görünür, bildirim kuyruğuna düşer, ekranda listelenir; etkileşim ve performans zinciri kanıtlanır.

Kabul koşulları:
1. Her kabul koşulu için Docker çıktısı ve ekran görüntüsü kanıtı.
2. Semih'in elle deneyebileceği senaryo listesi README'de.

## 6. Riskler ve açık maddeler

- KVKK: çalışan davranışı izlemesi için aydınlatma metni ve hukuki görüş gerekir (doğrulanmadı).
- Telegram bot hesabı sizin tarafınızdan açılmalıdır; V1-RMD-557 bu hesap gelene kadar bekler.
- `pg_stat_statements` eklentisi veritabanı yeniden başlatması gerektirir; ayrı ve açık bir dağıtım adımıdır.
- Ana makine (Docker Desktop/WSL2) disk ve işlemcisi konteynerden görünmez; ekranda bu sınır belirtilir.
- İzleme sisteminin kendi yükü ölçülmeden (V1-RMD-556) üretime alınmaz.

## 7. Kaynaklar

- Plan kayıtları: `plan/v1/remediation/V1-RMD-533…557`.
- Bağımsız plan denetimi (2026-10-15): çakışma, çöküş kanalı, gizlilik, ortak dosya ve ölçülebilirlik bulguları plana işlendi.
- Referans sistem: `D:\PROJECT\LOJINEXT` (`platform_infra/monitoring`).

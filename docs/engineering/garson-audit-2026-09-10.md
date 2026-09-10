# Garson ekranı bağımsız denetimi — 2026-09-10

> **Nasıl yapıldı:** Semih'in isteğiyle beş bağımsız ajan sırayla çalıştırıldı
> (backend, frontend, API uç noktaları, mimari sınırlar, veritabanı). Her
> ajana yalnız denetlenecek yüzey verildi, önceki turların sonuçları
> verilmedi; amaç birbirini tekrar etmeleri değil, birbirinin kaçırdığını
> bulmalarıydı. Veritabanı ajanı bulgularını scratch bir veritabanında SQL
> çalıştırarak kanıtladı.
>
> **Kayıt tarihi:** 2026-09-11 (V1-GOV-125).

## Özet

| Rapor | Bulgu | Kapandı | Açık |
| --- | ---: | ---: | ---: |
| Backend | 14 | 6 | 8 |
| Frontend | 37 | 7 | 30 |
| API uç noktaları | 18 | 7 | 11 |
| Mimari sınırlar (2 bulgu + 2 kör nokta) | 4 | 0 | 4 |
| Veritabanı | 11 | 0 | 11 |
| **Toplam** | **84** | **20** | **64** |

84 **ham** sayıdır: ajanlar birbirinden habersiz çalıştığı için bazı kusurlar
iki raporda birden var (aşağıda işaretli). Benzersiz sayı biraz daha azdır.

**Açık kalanın ağırlığı: 1 Critical, 5 High.** Critical'ın tamamı ve High'ların
dördü veritabanı raporunda; o blok bütünüyle açık.

---

## Kapanan bulgular (20)

### Para kaybettirenler

| # | Bulgu | Kapatan görev |
| --- | --- | --- |
| API-H2 / Backend-M5 *(çakışan)* | Garson istemcisi hiç `Draft` bırakmadığı için her tur **yeni bir sipariş** açıyordu; okuma yolu yalnız sonuncuyu döndürüyordu. ₺400 meze + ₺900 ana yemek söyleyen masada ₺400 hiç faturalanmıyordu | V1-ORD-006 |
| Backend-H1 | Birleştirme dalı DTO'ya artırılmamış `rowVersion` yazıyordu; ikinci tur "Gönder"e basınca 409 alıyordu | V1-ORD-006 |
| Backend-M1 | Birleştirme, ikinci tur not taşımıyorsa birinci turun sipariş notunu siliyordu (alerji notu dahil) | V1-ORD-006 |
| Backend-M2 / API-L4 *(çakışan)* | Birleştirme sipariş numarasını ikinci bir biçime yeniden yazıyordu | V1-ORD-006 |

### Güvenlik ve yetki

| # | Bulgu | Kapatan görev |
| --- | --- | --- |
| Frontend-C1 | `escapeHtml` tırnak kaçırmıyordu; ürün adı, masa numarası ve kalem notu çift tırnaklı niteliklere yazılıyordu. Menüye tırnaklı ad giren yönetici ya da QR'dan tırnaklı not yazan misafir garsonun oturumunda kod çalıştırabiliyordu | V1-RMD-153 |
| API-H1 / Backend-H3 *(çakışan)* | `KitchenState` hiç ilerlemediği için "zaten mutfağa gitmiş" duvarı ölüydü: yenmiş yemek `orders.create` yetkisiyle iptal edilebiliyor, `bills.void` onay akışı atlanıyor, bilet iptal edilmiyor, stok iade edilmiyordu | V1-RMD-154 |
| Backend-H2 | Her void/comp agregayı 21 argümanla yeniden kurup `servingUserId`'yi `null`'a düşürüyordu; masa sahipliği kontrolü devre dışı kalıyor, `transfer-server` siparişi devredemiyordu | V1-RMD-154 |

### Sipariş kaybı

| # | Bulgu | Kapatan görev |
| --- | --- | --- |
| Frontend-H3 | `flushQueue` yalnız `online` olayında çağrılıyordu; oysa kuyruğa girmenin asıl yolu sunucunun 5xx dönmesi ve o sırada ağ ayakta. Sipariş vardiya boyunca `localStorage`'da kalıyordu | V1-RMD-153 |
| Frontend-H2 | Masa değiştirince gönderilmemiş tur siliniyor, ekranda ise "duruyor" yazıyordu | V1-RMD-153 |
| Frontend-H5 | "Geri al" kapanışı masayı tutmuyordu; kalem başka masanın turuna düşebiliyordu | V1-RMD-153 |
| API-M1 *(kısmen)* | 429 kalıcı hata sayılıp kuyruktaki sipariş "hatalı"ya atılıyordu. **Açık kalan yarısı:** HTTPS geçidinin 400'ü aynı şeyi yapıyor | V1-RMD-153 |
| API-H3 | Cevabı kaybolan gönderim tekrarı 409 dönüyordu (hash `ExpectedRowVersion` içeriyordu, o da tekrarda zorunlu olarak değişiyor); istemci kalıcı hata sayıyor, garson yeniden giriyor, mutfağa aynı yemek ikinci kez gidiyordu | V1-RMD-155 |

### Kullanılabilirlik ve tutarlılık

| # | Bulgu | Kapatan görev |
| --- | --- | --- |
| Frontend-H6 | Adisyon toplamı sunucunun `TotalAmount`'ı yerine istemcide hesaplanıyordu; ikram/indirim sonrası masa kartıyla çelişiyordu | V1-RMD-153 |
| Frontend-M8, M9 | Adet basamağı ve not alanları 40px (foundations §3 asgari 48px) | V1-RMD-153 |
| API-M2 | Gönderilmemiş (Draft) kalemi iptal etmek imkânsızdı ve hata 409 "başka bir işlem değiştirdi" diye görünüyordu — hiçbir tekrar denemesi düzeltemezdi | V1-RMD-154 |
| API-D1 | `/void-sent`'in hiçbir istemcisi yoktu. V1-RMD-154 ucuz yolu kapatınca bu **acil** hale geldi: garson mutfağa gitmiş yemeği hiçbir şekilde iptal edemiyordu | V1-RMD-155 |

---

## Denetimde olmayan, düzeltme sırasında bulunan kusurlar (4)

Bunlar hiçbir raporda yok; V1-ORD-006 ve V1-RMD-155 çalışılırken çıktı.

1. **Mutfak sevkiyatçısı ikinci turu tamamen atlıyordu.** "Bu sipariş+istasyon
   için bilet var mı" diye soruyordu; bir sipariş = bir gönderim iken doğru
   bir tekrar korumasıydı, turlar birleşince birinci turun bileti bulunup
   ikinci tur hiç gönderilmiyordu. Soru artık ateşlenen kalemlere soruluyor.
   *Testin yakaladığı kusur.*
2. **Her tur aynı `operationId`'yi kullanıyordu** (`{orderId}:submit`). Turlar
   birleşince ikinci tur aynı anahtarla gelip 409 alıyordu.
   *Testlerim bunu maskelemişti:* yazdığım yardımcı her tura rastgele bir
   `operationId` veriyordu, yani gerçek istemciden sapıyordu. Yardımcı
   istemcinin şemasına çekildi ve eski şema geçici olarak geri konularak iki
   testin gerçekten düştüğü doğrulandı.
3. **`GetByIdAsync` yırtık okuma yapabiliyordu.** Sipariş, kalemler,
   eklentiler ve geçmiş dört ayrı sorguda, hiçbir işlem içinde olmadan
   okunuyordu; READ COMMITTED altında her ifade kendi anlık görüntüsünü aldığı
   için sipariş satırı commit öncesinden, kalemleri sonrasından gelebiliyordu.
   Eskiden zararsızdı; `FireRound` kalemlere de baktığı için sert hataya
   dönüştü. **Önce "build yarışı" diye açıklandı, yanlıştı:** tek başına 5'te 1
   düşüyordu, `git stash` ile temel sürümde 8/8 temiz çıktı. Dört okuma tek
   `RepeatableRead` anlık görüntüsüne alındı, 12/12 temiz.
4. **Masa boşaltılırken bayat `current_order_id` kalıyordu** — `SetAvailable`
   yalnız `current_status`'ü değiştiriyordu.

---

## Açık bulgular (64)

### Veritabanı — 11'inin tamamı açık, hepsi SQL çalıştırılarak kanıtlanmış

| Şiddet | Bulgu |
| --- | --- |
| **Critical** | `orders.order_items` miktar/fiyat işaretini zorlamıyor. `quantity=-3, unit_price=-10, gross=30` yazıldı, kabul edildi. Sıfır miktar ve negatif `tax_rate` de kabul ediliyor; `order_item_modifiers.quantity=-7` de öyle |
| **High** | `inventory.stock_movements.movement_type` ve `.direction` serbest metin, hiçbir CHECK yok — `('Banana','Sideways')` yazıldı. Tablo **değiştirilemez** (trigger), yani yanlış değer bir daha düzeltilemez |
| **High** | `product_stock_mappings.product_id` ve `modifier_stock_mappings.modifier_id` foreign key taşımıyor; olmayan ürüne eşleme yazılabiliyor ve stok sessizce hiç düşmüyor |
| **High** | `kitchen_ticket_items.order_item_id` ve `.product_id` foreign key taşımıyor; uydurma UUID'lerle satır yazıldı |
| **High** | `GetPendingOrdersAsync` indekssiz: 60.000 sipariş / 180.000 kalemde iki tam tablo taraması, 72 ms. Garsonun yokladığı uç nokta |
| Medium | `catalog.product_prices.price` negatif olabiliyor (`-15.00` yazıldı) |
| Medium | `order.json`'da `phaseBRange.max` hâlâ `095`, oysa 096 eklendi — **bu benim bıraktığım tutarsızlık** |
| Medium | `WithAvailableStockAsync` N+1: kalem başına 3 gidiş-dönüş |
| Low | Geri alma zinciri 11 boş şema bırakıyor |
| Low | İki migration `CREATE TABLE IF NOT EXISTS` kullanmıyor |
| Low | İki migration dosyası BOM ile başlıyor, `psql < dosya` ile kırılıyor |

### Frontend — 30 açık

**High:** başarısız gönderimden sonra miktar düzeltilirse mutfağa **eski
miktar** gidiyor ve ekran gönderilmiş gibi temizleniyor (sunucu, kalıcı bir
kalem kimliğini değiştirilemez sayıyor).

**Medium:** hatalı siparişler listesi ulaşılamaz (yalnız sayı görünüyor, ne
masa ne içerik, temizlenemiyor) · sayfalarda odak tuzağı, Escape ve `inert`
yok · PIN kilidi klavyeyi engellemiyor (Bluetooth klavyeyle perdenin arkasına
Tab'lanabiliyor) · "eklendi" animasyonu yok edilmiş düğüme uygulanıyor ve
klavye odağı kayboluyor · sunucunun ham `error.message`'ı doğrudan ekrana
basılıyor · Draft kalem "GÖNDERİLDİ" başlığı altında "Gönderilmedi" rozetiyle
görünüyor · düz HTTP'de şerit yanlış sebebi söylüyor · service worker
kaydı başarısızsa `enablePush` sessizce takılıyor · toast sayısı sınırsız,
9 kalemlik masada ekranı kaplıyor · gönderilmemiş tur yalnız bellekte, yeniden
yüklemede kayboluyor · çevrimdışıyken yeniden yükleme kuyruğu kullanılamaz
giriş ekranının arkasında bırakıyor · beş §0 ihlali (masanın dolu olduğu
paradan çıkarılıyor, devir hedefleri istemcide filtreleniyor, eklenti adedi
kuralı tekrar yazılıyor, zorunlu seçim yalnız istemcide zorlanıyor, `canVoid`
istemcide türetiliyor) · `CACHE_NAME` elle güncellenmezse eski uygulama
sonsuza kadar servis ediliyor.

**Low:** 12 madde (ölü CSS, `#userRole` hiç güncellenmiyor, katalog vardiya
boyunca yenilenmiyor, `nextCursor` yok sayılıyor, çift dokunma korumasız
onayla/reddet, menü her açılışta klavyeyi açıyor, canlı bölge eksikleri).

### API uç noktaları — 11 açık

Eklenti seçim kuralları sunucuda hiç zorlanmıyor (adet istemci kontrolünde bir
fiyat girdisi) · miktar, kalem sayısı ve not uzunluğu sınırsız; taşma kalıcı
hatayı **503** diye gösterip kuyruğun sonsuza kadar tekrar denemesine yol
açıyor · her `PostgresException` 503'e eşleniyor · masa yönetimi ve push
uç noktalarında hiç hız sınırı yok, `GET /orders/pending` ise yazma kovasında ·
katalog 1000'de sayfalanıyor, istemci `X-Next-Cursor`'ı okumuyor ·
`DELETE /push/subscriptions` çağırana göre kapsanmamış · `X-Idempotency-Key`
gönderiliyor ama kimse okumuyor · `waiterName`/`createdAt` yok sayılıyor ·
`GET /orders/{id}` yetki istemiyor ve terminale kapsanmamış · sipariş okumada
N+1 · `/comp` ve `/transfer-server`'ın hiçbir istemcisi yok.

### Backend — 8 açık

Negatif `price_delta` taşıyan bir eklenti o ürünü tamamen sipariş edilemez
yapıyor (katalog kabul ediyor, sipariş agregası reddediyor) · kasanın sabit
sahte masası iki müşterinin siparişini birleştirebiliyor · stok hareketinin
aktörü kasa satışında sıfır UUID · kalem sıralaması eşitlikte rastgele ·
mutfak geçişinde commit sonrası hata yanlışlıkla eşzamanlılık çakışması
diye raporlanıyor · `OrderMath.RoundQuantity` ölü · `PendingOrderConfirmationStore`
yorumu yanlış bir şey iddia ediyor · 3 ondalıktan fazla miktar her kayıtta
gereksiz sürüm artırıyor.

### Mimari sınırlar — 4 açık

`ModuleBoundaryTests` yalnız `src/Modules/**`'ı tarıyor; modüller arası yeni
bir bağ `src/Host/Experience/**` altına yazıldığında onaylı-kenar denetimi
hiç çalışmıyor. `OrderStockConsumptionService` ve `SentItemVoidStore`
Inventory/Kitchen/Billing agregalarını Host'tan orkestre ediyor — yazımlar
modülün kendi sözleşmesinden geçtiği için **özü** kurala uygun, ama bu kenarlar
`module-dependency-rules.md` tablosunda kayıtlı değil.

`consistency_audit.py` yalnız ham SQL'de şema adı arıyor; repository üzerinden
yapılan çapraz modül yazımını göremiyor. Bu sınırın `docs/CONSISTENCY_AUDIT.md`'de
yazılı olması gerekiyor.

Ölü istemci motorları: `WaiterManagerDecisionEngine` ve `WaiterOrderStatusEngine`
yalnız kendi test projelerine derleniyor, hiçbir yerden referans almıyor.
Gerçek istemci `waiter-app.js`. Daha önce iki kez silinen kalıbın
(`WaiterOfflineQueueEngine`, `OrderEntryEngine`) üçüncüsü.

---

## Bu denetimin duran iki yapısal eksiği

Bunlar tek tek bulgu değil, sistemin kavram eksiği:

1. **Hiçbir sipariş kapanmıyor.** `TransitionTo(OrderState.…)` çağrılarının
   tamamı tarandı: yalnız `Submitted`, `Accepted`/`Rejected` (sadece QR/NFC) ve
   `Cancelled` gerçekten çağrılıyor. `Preparing`, `Ready`, `Served`,
   `Completed` durumlarına geçen **tek bir çağrı yok**.
2. **Ödeme yok.** `BillState.Paid` tanımlı ama Host'ta hiçbir şey bir hesabı
   ödenmiş yapmıyor. Bilinen ve kasıtlı bir V1.2 sınırı; sonucu
   `docs/design/modules/check-and-table.md`'de kayıtlı: "ödeme bekleyen
   hesaplar" listesi kendiliğinden boşalmaz.

## Sonraki adım için öneri

Veritabanı bloğu. Tek Critical orada, dört High'ın dördü orada, ve dördü
(işaret kısıtları + üç foreign key) tek bir migration'la kapanabilir.

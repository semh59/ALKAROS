# V12-GOV-006 karar kaydı - Online yemek platformlarına doğrudan entegrasyon

- Karar tarihi: 2026-09-26
- Onaylayan: Semih (Founder/Product Owner): "Doğrudan entegre edeceğiz"
- Hazırlayan: Claude Opus 5.5 (session 703155c9)

## Pazar durumu (erişim 2026-09-26)

- Uber, 2025'te Trendyol Go'nun kontrolünü aldı. Rekabet Kurulu 19 Haziran 2026'da Getir'in yemek ve market
  teslimatı işlerinin devrine şartlı onay verdi.
- Eylül 2026'dan itibaren Getir Yemek ayrı satıcı paneli olarak yönetilmiyor; operasyon Uber Eats Trendyol Go'ya
  taşınıyor. Arama sonuçlarında Getir geliştirici belgesinin GetirYemek API uygulamalarının sonlandırıldığını
  söylediği görüldü (sayfanın kendisi bu oturumda okunamadı).
- Restoranın yöneteceği kanallar: Yemeksepeti, Uber Eats Trendyol Go, Migros Yemek.

## Platform erişim yolları

| Platform | Belge | Erişim | ALKAROS durumu |
| --- | --- | --- | --- |
| Yemeksepeti | Herkese açık Partner API v2.0.2 | Partner Portal bilgileri ve sandbox | Kod yazıldı, doğrulanmamış taslak (`V0-YSP-001` `Blocked`) |
| Uber Eats Trendyol Go | Herkese açık (`developers.tgoapps.com`) | Satıcı panelindeki entegrasyon bilgileri; stage ve canlı ortam | Plan: `V12-TGO-001..005` |
| Migros Yemek | Herkese açık belge yok | Restoran panelinde listelenen POS entegratörleri API anahtarıyla bağlanıyor | Plan: `V12-MGY-001..002` |

## Uber Eats Trendyol Go belgesinden okunanlar

- Kimlik doğrulama: basic auth; supplierId, API key, API secret satıcı panelinde "Hesap Bilgilerim → Entegrasyon
  Bilgileri" sayfasında. Stage ve canlı bilgiler ayrı. Hatalı bilgide 401.
- Her istekte `User-Agent: "{sellerId} - {entegratör adı}"`; yoksa 403. Durum çağrılarında `x-agentname` ve
  `x-executor-user` başlıkları.
- Aynı uç noktaya 10 saniyede en çok 50 istek; aşılırsa 429.
- Adresler: `https://api.tgoapis.com` (canlı), `https://stageapi.tgoapis.com` (stage); stage'de test siparişi servisi var.
- Webhook: tek entegratör tanımı, satıcılar bu tanıma eklenir; olaylar `created`, `pickupEtaCalculated`,
  `courierNearby`, `shipped`, `delivered`, `cancelled`, `unsupplied`, `storeChanged`, `sellerChanged`. Başarısız
  teslim en çok 3 kez yeniden denenir. Alıcı uzun süre kapalı kalırsa Trendyol entegrasyonu kapatabilir.
- Paket durumları: Created, Picking (restoran kabulü, `preparationTime` ile), Invoiced (restoran hazır), Shipped,
  Delivered, Cancelled (restoran dışı iptal), UnSupplied (restoran iptali; nedenler 621–627).
- Kendi kuryesiyle dağıtan restoran için manuel "yola çıktı" ve "teslim edildi" çağrıları.
- Menü: menü okuma, bölüm ve ürün aktif/pasif, kuyruğa alınan fiyat güncellemesi ve `batchRequestId` sonucu.
  Stok adedi gönderilmez.
- `created` yükünde yemek kartı ödeme bilgisi (`payment.mealCard`), kupon ve promosyonlar var.
- Uber Eats geçişi (ilk aşamada yalnız Bilecik mağazaları): sipariş kodu 3'ten 5 karaktere, adres alanlarında
  maskeleme, siparişlerin otomatik `Invoiced` olması, dinamik telefon ve PIN. Diğer şehirlerin takvimi açıklanmadı.

## Seçilen sonuç

Her platform kendi resmî arayüzüyle doğrudan bağlanır. Önce ortak çekirdek kurulur (platform ayrımı, adaptör
sözleşmesi, ortak gelen kutusu ve eşleme, çekme altyapısı, platform bazlı mutabakat, platform etiketi ve kimlik
bilgisi ekranı); platform adaptörleri bu çekirdeğe bağlanır.

## Reddedilen alternatifler

- Aracı entegrasyon firmaları (tek REST köprüsü): üçüncü taraf bağımlılığı ve ücret getirir, müşteri verisi başka
  bir firmadan geçer (KVKK). Semih doğrudan entegrasyonu seçti.
- Getir Yemek için ayrı adaptör: platform Uber Eats Trendyol Go'ya devredildi.
- Yeni dış sözleşme görevlerini V0'da açmak: yeni `Blocked` V0 görevi `GATE-V0-EXIT` türetimini etkilerdi;
  görevler kanalın yaşadığı V12'de açıldı.
- Belgesiz Migros Yemek kodu: `TASK_STANDARD.md` belgede doğrulanmayan API davranışını yasaklıyor.

## Etkilenen görevler

`V12-ONL-006`, `V12-ONL-007`, `V12-ONL-008`, `V12-ONL-009`, `V12-REC-002`, `V12-OUI-002`, `V12-TGO-001`,
`V12-TGO-002`, `V12-TGO-003`, `V12-TGO-004`, `V12-TGO-005`, `V12-MGY-001`, `V12-MGY-002`.

## Kaynaklar

- Uber Eats Trendyol Go Developers: <https://developers.tgoapps.com/en/docs/overview>,
  `/en/docs/authorization`, `/en/docs/trendyol-go-meal/webhook-order-integration/Introduction`,
  `/en/docs/trendyol-go-meal/webhook-order-integration/Event-Types-Payloads`,
  `/en/docs/trendyol-go-meal/order-integration/package-models`,
  `/en/docs/trendyol-go-meal/order-integration/accepting-order`, `/en/docs/category/menu-integration`,
  `/en/docs/uber-eats-migration` (erişim 2026-09-26).
- Getir Developers: <https://developers.getir.com/food/documentation/giris> (erişim 2026-09-26).
- Rekabet Kurulu onayı: <https://www.donanimhaber.com/rekabet-kurumu-onayladi-uber-getir-i-satin-aldi--207061>.
- Getir Yemek ve Trendyol Go birleşmesi: <https://enisyavuz.com.tr/p/trendyol-go-ve-getiryemek-tek-cat>,
  <https://www.elyazmalari.com/2026/08/29/uber-eats-turkiyede-buyumuyor-pazari-topluyor/>.
- Migros Yemek POS entegrasyonu: <https://bilgibankasi.akinsoft.net/tr/home/makale/3691-migros-yemek-entegrasyonu-yardim-dokumani>,
  <https://www.apimerkezi.com/migros-yemek-entegrasyonu.html>.

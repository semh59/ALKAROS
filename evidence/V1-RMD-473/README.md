# V1-RMD-473 - Yönetim: Online faturalar bölümü

Yönetici, online siparişlerden açılan fatura taslaklarını ve taslağı henüz açılamamış siparişleri Yönetim alanında görür. Yalnız okur; gönderim yoktur.

## Değişiklikler

- `src/Host/Experience/OrderInvoices/OrderInvoiceEndpoints.cs`: `GET /api/v1/management/order-invoices?from&to` (hizmet tarihine göre taslaklar, en çok 31 gün, ayrıca son 7 günde teslim edilip
  taslağı olmayan siparişler ve kalan gün) ve `GET .../by-order/{orderId}` (satıcı, web adresi, kalemler; yoksa Türkçe 404). Yönetici oturumu + `reports.view`
  (kanal raporuyla aynı süzgeç). `DualScreenApplication.cs` kaydı.
- `src/Clients/PosTerminal/src/features/management-order-invoices/`: API istemcisi, bölüm bileşimi, css. `sections.ts` kaydı, `strings.ts` > `managementText.orderInvoices`.
  Platform etiketi ve tarih yardımcıları kanal raporundan yeniden kullanıldı.

## Plandan sapma

- "Alıcı bilgisi eksik" uyarısı yok: eşik tutarı resmî metinle doğrulanmadı (`V1-RMD-470` ile aynı karar).
- Uç nokta testleri yeni bir test projesi açmak yerine `tests/Host/Experience/Reconciliation/OrderInvoiceHttpTests.cs` içinde (yönetici oturumu ve gerçek modül bileşimi orada hazır).
- Faturasız sipariş listesi vaka beklemez; uzlaştırma vakası `V1-RMD-475`.

## Kanıt

- `green-host.log`: 5 yeni uç nokta testi (oturum yok 401, izinsiz 403, liste aralığı + faturasız sipariş + kalan gün, sipariş başına ayrıntı ve 404, ters ya da 31 günü aşan aralık 400).
- `mutation-host.log`: aralık sınırı, 7 günlük pencere ve kalan gün hesabı bozulunca 2 test kırmızı; dosya geri alındı, hash özdeş.
- `vitest.log` 58 dosya / 433 test yeşil (9 yeni: sekme yetkisi, liste ve Türkçe etiketler, axe, faturasız bölümü, ayrıntı, tarih gönderimi, aralık reddi, boş liste, sunucu hatası, adresler);
  `typecheck.log`, `lint.log`, `build.log` exit 0.
- `mutation-client.log`: sekme yetkisi, faturasız bölüm koşulu, ayrıntı adresi ve durum etiketi bozulunca 5 test kırmızı; dosyalar geri alındı, `cmp` özdeş.
- `trial.live.log` + `trial.png`: gerçek Host ve Postgres, tarayıcıda Yönetim > Online faturalar: satıcı bilgisi yokken teslim edilen sipariş "faturası henüz açılmamış" listesinde (7 gün kaldı),
  bilgi girildikten sonra teslim edilen sipariş taslak olarak listede (net 181,82, KDV 18,18, toplam 200,00); "Göster" satıcıyı, adresi ve kalemi açtı.

## Açık kalan

- Gönderim yok (`V0-QNB-001` engelli); ekranda "taslaktır, gönderim henüz yapılmaz" yazar.

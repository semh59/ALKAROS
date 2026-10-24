# qa-dokuman-bagimlilik - V1-RMD-602 ham rapor (özet biçim)

Kontrol: 142/142 kalem yol varlığı, doküman atıfı ve bağımlılık/lisans yönünden (işlev derin okuması diğer ajanlarda). Kanıt düzeyi Kod okuması.

| No | Önem | Kanıt | Sorun |
| --- | --- | --- | --- |
| D1 | Orta | canonical-value-catalog.md:62; OrderEnums.cs OrderSource.Nfc; 077-orders-nfc-source.up.sql:3 | Doküman sapması: kanonik katalogda `Nfc` kaynağı yok. |
| D2 | Orta | canonical-value-catalog.md:64; 105-order-items-course.up.sql:27; KitchenState.Held | Doküman sapması: `Held` durumu katalogda ve geçiş sözleşmesinde yok. |
| D3 | Orta | single-branch-key-strategy.md:18 (`ORD-{yyMMddHHmm}-{seq}`); TableDraftService.cs:191 (`TBL-`), NfcOrderingStore.cs:163 (`NFC-`), QrOrderSubmittedConsumer.cs:106 (`QR-`) | `ORD-` üreten kod yok, üç kanal üç ön ek; NFC `{HHmmssff}` aynı masada aynı yüzde-saniyede çakışabilir (orders_order_number_key yakalar; ölçülmedi). |
| D4 | Düşük | module-dependency-rules.md:172-174; OrderStockConsumptionService.cs:6 `using ALKAROS.Measurements`; ModuleBoundaryTests.cs:275-280 | Measurements izin listesinde/dokümanda yok (ortak kütüphane olabilir; doğrulanamadı). |
| D5 | Orta | module-dependency-rules.md:49 | `OrderStateChanged` olayı dokümanda var, kodda yok (IntegrationContracts yalnız Kitchen, QrOrder, Table). |
| D6 | Düşük | api-contract-standard.md:97-115; OrderManagementEndpoints.cs:52,262 | Örnek sözleşme gerçek yola ve anahtar yerine (gövdede IdempotencyKey) uymuyor. |
| D7 | Düşük | void-complimentary-discount-policy.md:31,81; OrderManagementEndpoints.cs:597-598,768 | Tablo "her iptal Manager yetkisi" der; gönderilmemiş satır iptali yalnız OrdersCreate ister (PO sorusu). |
| D8 | Düşük | plan/v1/orders V1-ORD-005, 006; evidence klasörü yok | İki Done görevin evidence klasörü yok (tarihsel). |
| D9 | Düşük | ALKAROS.Orders.csproj:11-14 | Kod yorumunda görev ID'si ve tarih. |
| D10 | Düşük | projection-ownership.md:19,21 | `bill_order_items` adı kanonik `bill_items` ile tutarsız olabilir (doğrulanmadı). |

Temiz: dosya yolu atıfları (0 eksik), Orders csproj referansları, paket/lisans (Npgsql onaylı, FluentAssertions 6.12 politikada, axe-core MPL izinli, GPL/AGPL yok), enum/CHECK uyumu (D1, D2 dışında), using kökleri (Measurements dışında).

Doğrulanamayan: geçişli bağımlılık lisansları, plan_audit koşulmadı, D3 çakışma riski, D4 Measurements'in modül sayılıp sayılmadığı.

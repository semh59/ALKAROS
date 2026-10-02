# V1-RMD-492 - QNB gelen kutusundan alış faturası çekme

Yönetici "QNB'den çek" dediğinde ALKAROS, QNB eSolutions gelen kutusundaki faturaları listeler, UBL olarak indirir ve V1-RMD-489 hattıyla taslak alış faturası yapar. Onay hep yöneticidedir (V1-RMD-490).

## Sözleşme durumu (revize edilebilir)

- QNB sözleşmesi `evidence/v0/integrations/V0-QNB-001` kod kütüphanesindendir: `connectorService.gelenBelgeleriListeleExt` (belgeTuru FATURA, `sonAlinanBelgeSiraNumarasi`, `vergiTcKimlikNo`) ve `gelenBelgeleriIndirExt` (UBL, ETTN). Liste cevabı kütüphanedeki örnekle aynı biçimde ayrıştırılır.
- Canlı QNB sunucusunda yalnız `wsLogin` doğrulanmıştır (V14-QNB-007). İndirme cevabının biçimi (base64, zip içinde UBL) kütüphanenin diğer indirme metodundan varsayılmıştır; çıplak XML de kabul edilir. QNB'den farklı bir şey çıkarsa yalnız `QnbSoapClient.DownloadIncomingInvoiceXmlAsync` revize edilir.
- Kimlik bilgisi mevcut QNB kayıt ekranından gelir (`IQnbCredentialStore`); adres `ALKAROS_QNB_USER_SERVICE_URL` ile değişir. Zamanlanmış otomatik çekme yoktur: doğrulanmamış sözleşmeyle kendiliğinden QNB trafiği üretmemek için yalnız elle çekilir.

## Uç nokta

`POST /api/v1/management/purchasing/purchase-invoices/fetch-qnb` (`purchasing.manage`) -> `{ listed, imported, duplicates, skipped, stoppedEarly }`.

## Kurallar

- Son alınan sıra numarası `purchasing.purchase_invoice_inbox_cursors` tablosunda tutulur (migration 177); imleç geri gitmez. Her sayfa en çok 100 belge, bir çekmede en çok 10 sayfa.
- Belge bazında yalıtım: yinelenen ETTN `duplicates`, iade/bozuk/QNB'nin vermediği belge `skipped` sayılır ve imleç bunları geçer. Bağlantı hatası (zaman aşımı, ağ) çekmeyi durdurur (`stoppedEarly`), imleç son başarılı belgede kalır, sonraki çekme o belgeden devam eder.
- Kimlik bilgisi yoksa 409 `QNB_NOT_CONFIGURED`; QNB girişi reddederse veya ulaşılamazsa 503 `QNB_UNAVAILABLE` (teknik ayrıntı kullanıcıya gösterilmez).
- Ekranda "QNB'den çek" düğmesi sonucu Türkçe özetler ve listeyi yeniler.

## Kanıt

- `tests-qnb-client.log`: 12 test (7 yeni: connectorService adresi, devam noktası, alanlar, boş liste, sıra numarasız belge reddi, zip açma, çıplak XML, boş/bozuk cevap).
- `tests-module.log`: 34 test (7 yeni: yeni belge+imleç, yinelenen, iade/bozuk atlama, geçici hatada durma ve devam, çok sayfa, imleç geri gitmez, ayarsız kaynak).
- `mutation.log`: 10 mutant hepsi yakalandı; dosyalar hash ile geri yüklendi.
- `migration-up-down.log`: 177 ileri, geri, yeniden ileri, ikinci up.
- `gercek-deneme.log`: gerçek Host + Postgres + yerel sahte QNB sunucusu: ayarsız 409, kayıt, elle yüklenmiş belge yinelenen sayılır, iade atlanır, ikinci çekmede 0, yeni belge `after=5003`'ten sonra gelir, giriş reddinde 503, imleç 5004, istek dökümü.
- `tests-ui.log` (463/463, 3 yeni), `typecheck.log`, `lint-build.log`, `tests-architecture-api.log` (5/5), `tests-architecture-modules.log` (9/9), `tests-host.log` (manifest dahil tam Host).

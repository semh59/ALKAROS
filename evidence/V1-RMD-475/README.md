# V1-RMD-475 - Faturasız kalan online sipariş için uzlaştırma vakası

Teslim edilip (Served/Completed) son 7 günde kapanmış, 1 saatten eski ve fatura taslağı olmayan online sipariş, Sorunlar sekmesinde vaka olarak görünür.
Satıcı bilgisi yoksa önerilen adım "İşletme bilgilerini girin", varsa "Faturayı elle düzenleyin"; yeniden deneme yok; taslak açılınca vaka kendiliğinden çözülür; atlanan vaka yeniden açılmaz.

## Değişiklikler

- `src/Modules/Reconciliation/OnlineOrders/MissingInvoiceSourcePair.cs` (yeni), `OnlineOrderReconciliationModels.cs` (tür + iki eylem), `OnlineOrderReconciliationModule.cs` (kayıt).
- İstemci: `onlineProblemsApi.ts` Türkçe etiketler. Host testinde kaynak çifti sayısı 10 -> 11.

## Kanıt

- `green-module.log` (40 test yeşil, 6 yeni), `green-host.log` (28 yeşil), `vitest.log`, `typecheck.log`, `lint.log` exit 0.
- `mutation-module.log`, `mutation-client.log`: bozulan her mutantta 1 test kırmızı, dosyalar geri alındı.
- `trial.live.log`: gerçek Host + Postgres; 2 saat önce teslim edilmiş taslaksız sipariş zamanlanmış taramayla vaka oldu, Sorunlar sekmesinde Türkçe metin ve "Önerilen: İşletme bilgilerini girin" göründü, "Yeniden dene" düğmesi yok.

## Açık kalan

- Denemenin son adımı (bilgi girince taslak + vaka kapanışı) oturum kesilince tamamlanmadı; bu davranış `TheCaseResolvesOnlyAfterTheDraftExists` testiyle kanıtlı. Ekran görüntüsü alınamadı.
- Vaka yalnız 7 günlük pencerede listelenir; süre dolan sipariş listeden düşer (Yönetim > Online faturalar aynı pencereyi kullanır).

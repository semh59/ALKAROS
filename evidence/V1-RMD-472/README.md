# V1-RMD-472 - Teslimde online sipariş fatura taslağı

Online sipariş teslim edilince (Yemeksepeti ve Trendyol Go aynı `hand-over` ucunu kullanır) siparişin e-Arşiv fatura taslağı açılır. Taslak başarısız olsa da teslim başarılıdır.

## Değişiklikler

- `src/Host/Experience/OnlineOrdering/OnlineOrderInvoiceDrafting.cs`: sipariş ve sayılan kalemleri okuyup `IOrderInvoiceDraftService`e verir (tutarlar siparişte saklandığı gibi, KDV dahil).
  `TryDraftAsync` fatura hatasında atmaz, uyarı günlüğü yazar; `DraftMissingAsync` son 7 günde teslim edilmiş ve taslağı olmayan siparişleri tamamlar (her geçişte en çok 50).
  Web adresi platformdan gelir; ödeme türü, ödeme tarihi ve taşıyıcı `V1-RMD-474` gelene kadar boş.
- `OnlineOperationsEndpoints.cs`: `hand-over` sonucu `Applied` ise taslak çağrılır (tekrar teslim `AlreadyApplied` döner, yeni taslak açmaz); servis kayıtları.
- `OnlineOrderReconciliationHostedService.cs`: her geçişte faturasız siparişleri tamamlar (5 dakikada bir).
- Kullanılmayan-servis izin listesinden taslak servisinin iki kaydı çıktı.

## Plandan sapma

- Plan "yaklaşan 7 gün için uzlaştırma vakası" da içeriyordu; ayrı görev olarak ayrıldı (`V1-RMD-475`), çünkü kaynak çifti, vaka ayrıntısı ve istemci etiketi gerektiriyor.
- Kanca `YemeksepetiStatusSyncService` içinde değil uçta: servis yapıcısı değişmedi, başka test ve kullanıcılar etkilenmedi. `hand-over` tek çağıran.
- Taramanın testleri `OnlineOperationsHttpTests` içinde (kabul edilmiş gerçek sipariş yardımcısı orada); `OnlineOrderInvoiceDraftingTests.cs` açılmadı.
- Platform web adresleri (`yemeksepeti.com`, `trendyol.com`) sabit; muhasebeci doğrulamalı.

## Kanıt

- `green-host.log`: OnlineOrdering Host testleri 177/177 (3 yeni: teslimde taslak ve tekrar teslim, satıcı bilgisi yokken teslim başarılı + tarama sonradan tamamlar + ikinci geçiş 0,
  8 günlük sipariş taranmaz + Trendyol Go adresi). `green-reconciliation.log`: 23/23.
- `mutation-host.log`, `mutation-host-catch.log`: uçtaki çağrı koşulu, 7 günlük pencere ve `catch` yutması bozulunca 2 + 3 test kırmızı; dosyalar geri alındı, hash özdeş.
- `trial.live.log`: gerçek Host ve Postgres: Yemeksepeti webhook'u ile 2 × 100 TL sipariş. Satıcı bilgisi yokken teslim 200, taslak yok; bilgi kaydedilince ikinci sipariş teslimde anında taslak
  (200,00 TL, KDV 18,18, `Draft`); zamanlanmış geçiş birinci siparişin taslağını da açtı (`created_by` boş).

## Açık kalan

- Taslak kesilmiş fatura değildir; QNB gönderimi gelene kadar 7 günlük süre için fiş ya da elle fatura gerekir. Süre uyarısı `V1-RMD-475`, liste ekranı `V1-RMD-473`.

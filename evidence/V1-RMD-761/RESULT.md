# V1-RMD-761 sonuç

Bulgu (V1-RMD-742 denetimi): yeniden kurmada `DELETE`, tek tek yazmalar ve rezervasyon geri yüklemesi ayrı otomatik-commit ifadeleriydi; yazma sırasında hata olursa eski bakiyeler silinmiş kalıyordu.

Düzeltme: `IStockBalanceRepository.ReplaceAllBalancesAsync` (arayüzde eski sıralı çağrılarla varsayılan gövde; sahte depolar değişmedi). Postgres tek transaction'da rezervasyonları okur, siler, yazar, rezervasyonu geri yükler, commit eder. `RebuildAllBalancesAsync` yeni metodu çağırır.

## Kanıt
- Kırmızı (`red/`): yazma sırasında kısıt ihlali (var olmayan stok kalemi) sonrası bakiye 42 yerine yarım yazılmış 10 kaldı.
- Yeşil (`green/`): `BalanceProjection` testleri 17/17, exit 0 (rezervasyonun yeniden kurmada korunduğu test dahil). Host ve ManualAdjustments testi derlemesi 0 uyarı 0 hata (`build-check.log`).
- Mutasyon (`mutation-no-override/`): Postgres geçersiz kılması devre dışı kalınca (varsayılan sıralı gövde) yeni test düştü; kaynak `cmp` ile geri yüklendi.

## Sınırlar
- Eşzamanlı hareket yazımı ile yeniden kurma yarışı kapsam dışıdır; koruma yalnız hata anında atomikliktir.
- Metodun üretimde çağıranı yoktur; düzeltme bir bakım aracı bağlandığında veri kaybını önler.

## İnceleme
- Kod ve test incelemesi, engelleyici bulgu yok. Düşük notlar: test yalnız genel `PostgresException` bekler (SqlState 23503 doğrulamaz); mutasyon yalnız "override'ı kaldır" mutantını dener; rezervasyon okuması kilitsiz olduğundan okuma ile silme arasında eklenen bir rezervasyon kaybolabilir (eski kodda daha açıktı).

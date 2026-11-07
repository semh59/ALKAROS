# V1-RMD-884 sonucu

İndirimli hesapta ürün bazında bölme artık 409 vermiyor; 879'daki kalem indirim dağıtımıyla çalışıyor.
- Motor: `SplitEngine.CreateItemSplit(..., adjustment)`. Her kalem `AdjustmentCalculator.DiscountedLineAmounts` tutarıyla, KDV'si düzeltilmiş KDV toplamının kalemlerin kendi KDV'sine göre en büyük kalıntı dağıtımıyla bölünür. Tüm kalemler dağıtılınca toplam ödenecek tutara, KDV düzeltilmiş KDV toplamına kuruşu kuruşuna eşit (domain testi: 3x33,33 + 40, indirim 25,01 -> 114,98; yüzde + tutar indirimi birlikte).
- Mağaza: ek ücret veya bahşiş varsa ve ödenecek tutar/KDV değişiyorsa 409 `ITEM_SPLIT_ON_ADJUSTED_BILL` kalır (ek ücret/bahşiş kaleme ait değil); sıfır-net durumda (bahşiş = indirim, KDV 0) eski davranış. Hata metni "Ek ücret veya bahşiş eklenmiş hesap ürün bazında bölünemez..." (sunucu, PosTerminal, Cashier).
- Ekran (PosTerminal): "ürün bazında bölme kullanılamaz" uyarısı artık yalnız ödenecek tutar kalem toplamından büyükse (ek ücret/bahşiş) çıkar; indirimli hesapta çıkmaz.
- Eşit bölmede kuruş kalıntısı son kişide (Semih kararı) zaten böyleydi; test eklendi (100 TL / 3 = 33,33 + 33,33 + 33,34).

## Kanıt
- Gerçek Postgres: SplitDesign 44 test (green-dom2.log, final-dom.log), Host.Experience.Billing tam proje (final-host.log, bkz. test koşusu notu); vitest billing 81/81, tsc ve oxlint temiz.
- Mutasyon (mutations.txt): 9 mutant (A1-A7 .NET, B1-B2 vitest), hepsi öldü; mutasyondan sonra yeniden derlenmiş son koşular.

## Açık risk / kabul edilenler
- Ödenecek tutarı sıfırlayan veya bir kalemi 0,00'a düşüren çok yüksek indirimde (veya çok küçük kalemde, kalıntı son hedefe sıfır bırakırsa) ürün bölme genel 400 `VALIDATION_FAILED` verir; indirim yokken de mümkün olan eski davranış, tam indirimde hesap zaten kapanır.
- Bölme kaydedildikten sonra indirim eklenir/geri alınırsa kayıtlı tasarım bayatlar (tüm modlarda önceden var); bir sonraki kayıtta doğrulama yakalar.
- Ekran uyarısı indirim + bahşiş birlikte ve toplam kalem toplamının altında kalıyorsa görünmez; sunucu 409 ile Türkçe açıklar (sunucudan bayrak taşımak DTO değişikliği gerektirirdi).
- Kalem bazlı indirim (`BillAdjustment.BillItemId`) hesaplayıcıda okunmuyor; indirim her zaman tüm kalemlere orantılı dağıtılıyor (879 davranışı, iade ve KDV dökümüyle tutarlı).

## Test koşusu notu
`final-host.log` (Host.Experience.Billing tam proje, 14 dk): 280 başarılı, 1 başarısız: `MergedBillOrderLinkTests.TheTillQueueShowsTheMergedBillOnItsOrdersAndDropsThemOncePaid` ("Expected 2, Actual 1"). Bu bölme koduyla ilgisiz bayat bir test: 871 birleşik hesabı kasa kuyruğunda tek satır yaptı, test hâlâ iki satır bekliyor (`merged-rerun.log` tek başına da aynı sonucu verir; `CashierHandoffStore` bu görevde değişmedi). Ayrı bir görevde düzeltilecek.

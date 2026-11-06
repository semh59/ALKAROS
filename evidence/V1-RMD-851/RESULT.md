# V1-RMD-851 sonuç

## Kök neden
Her test dosyası uygulamayı `vi.resetModules()` ile baştan yükler; bir dosyanın ilk yüklemesi bütün modül grafiğinin dönüşüm maliyetini öder. Tam koşuda otuz üç dosyanın on iki işçisi aynı anda bu dönüşümleri ister; maliyet işlemci çekişmesiyle çok büyür. Ölçümler (ham çıktı `measure-*` dosyalarında): `waiter-app.test.js` ilk testi tek başına 506 ms (`measure-waiter-alone.log`), 24 yük işlemiyle 2164 ms (`measure-waiter-24load.log`), 40 yük işlemiyle 2456 ms (`green-waiter-40load.log`). Yüksüz tam koşuda (`measure-full-unloaded.json`) `waiter-half-portion-stepper` ilk testi 5870 ms sürdü (dosyanın kendi 30 sn süresiyle geçti); doymuş koşularda aynı test 9887 ms ve 12143 ms (`red-saturated-2500ms.json`, `red-saturated-4s.json`). Yani soğuk yükleme tam koşuda ve çekişmede büyük ölçüde yavaşlıyor; 5 sn varsayılan bu payı karşılamıyor.

## Yeniden üretim
- Varsayılan 5 sn ve 40 yük işlemiyle tam koşu (`red-default-5s-40load.json`, `red.log`): `waiter-app.test.js`'in ilk üç testi 5026, 5008 ve 5215 ms ile zaman aşımına düştü; bildirilen hata birebir. (Aynı yükte iki test dosyası da başlayamadı; 40 yükte tam koşu hiç yeşile çıkmadı, bu yüzden düzeltmenin yeşili tek dosyada ve 24 yükte tam koşuda gösterildi.)
- Süre sınırı 2,5 sn'ye çekilip 24 yük işlemiyle düşen 6 test yalnız soğuk yükleme testleridir (waiter-app'in ilk üçü, `split-payment-item-refund`): `red-saturated-2500ms.json`. 24 yük ve 4 sn sınırıyla 370/370 geçti (`red-saturated-4s.json`).

## Düzeltme
`vitest.config.js`: `testTimeout` ve `hookTimeout` 30 sn (dosya düzeyinde zaten kullanılan 30 sn ile aynı, ölçülen en kötü 12 sn'nin 2,5 katı). İşçi sayısı sınırlanmadı; takılan test hâlâ düşer.

## Kanıt
- Yüksüz tam koşu `green.log`: 33 dosya, 370 test. 24 yük işlemiyle tam koşu `green-saturated.log`: 370/370.
- Aynı 40 yükle `waiter-app.test.js` (`green-waiter-40load.log`): 13/13, ilk test 2,5 sn.
- Mutasyon `mutations.log`: yapılandırma varsayılana döndürülünce aynı yükte üç zaman aşımı geri gelir (`red-default-5s-40load.json`); yapılandırma cp+cmp ile geri alındı.

## Açık notlar
- 30 sn sınırı kök nedeni (soğuk yükleme maliyeti) ortadan kaldırmaz; yalnız payı verir. Dosya başına `beforeAll` ile ısıtma ayrı dosyalara dokunur ve bu görevin yüzeyi dışındadır.
- Plan, kırmızıyı 4 sn kısıtıyla 24 yükte bekliyordu; 4 sn yetti (370/370). Kırmızı bunun yerine bildirilen hatanın kendisiyle (varsayılan 5 sn, 40 yük) üretildi.
- 30 sn sınırı 5-30 sn arası takılmaları geç yakalar; takılan test yine düşer.

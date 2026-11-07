# V1-RMD-897 sonucu

Şüphe GERÇEK çıktı: ödemeleri 95,99 + 144,00 olan, 10,01 indirimli hesapta kalemler tek tek iade edilince toplam 239,98 (ödenen 239,99). İlk ödemeye sığmayan kuruş `Math.Min` ile kayboluyor, diğer ödemede kalıyor (red-1.log).

## Düzeltme
`PaymentRefunds.PriceItemsAsync`: kalem iadesi, daha önce iade edilen kalemlerin kümülatif payı ile bu tür isteklerde verilen para arasındaki farkı (`PostgresRefundRequestRepository.ItemRefundedAmountAsync`) bir sonraki kalem iadesine ekler. Ödeme kalanı sınırı ve kalem olmayan (tutar) iadeler değişmedi.

## Kanıt
- Yeşil: green-1.log; PaymentRefundHttpTests 82/82 (final-refund.log); komşu iade/iptal sınıfları 21/21 (final-other.log).
- Testler: ItemRefundRoundingHttpTests.cs (4 test): iki ödemede toplam 239,99; kuruş tam ödeme iadesinden sonra da eklenir (95,99 sonra 96,00); iki kez kalan kuruş son iadeye eklenir ve önceki iadeyi reddettirmez; reddedilen iade borç bırakmaz.
- Mutantlar (mutations.txt, hepsi son kodla): tolerans carry içermiyor, reddedilen sayılıyor, kalem payı atlanıyor, verilen para düşülmüyor/yok sayılıyor, tutar iadeleri sayılıyor: 6 mutant öldü. Tolerans düzeltmesi (ilk tur bulgusu) ve reddedilen istek testi ikinci turda eklendi.

## Açık
Bir kalem iki ödemeye bölünemez: kalan, kalem tutarından küçükse istek REFUND_AMOUNT_EXCEEDS_PAYMENT ile reddedilir; kullanıcı kalan tutarı kalemsiz iade eder (davranış değişmedi). Ödeme sonrası indirimle gelen fark 898'de.
- Etki notları: 879 öncesi kayıtlı kalem iadelerinin kuruş farkları bir sonraki kalem iadesinde carry olarak ödenir. İadeden sonra eklenen indirim carry'yi eksiye çekebilir (sonraki kalem iadesi o farkı keser); bu davranış V1-RMD-898'in konusudur ve orada ele alınır.

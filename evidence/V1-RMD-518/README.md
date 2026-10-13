# V1-RMD-518 kanıtı

Sonuç: V1-RMD-506/508 ile eklenen beş veri değiştiren uç idempotency kaydına gerekçeyle eklendi; `EveryMutatingEndpointTakesAnIdempotencyKey` yeniden geçer.

## Bulgu
- Bu testin başarısız olduğu V1-RMD-506/508 kapanışlarında fark edilmedi (ilgili test projesi o görevlerde koşulmamıştı). Düzeltme bu görevde yapıldı.

## Açık
- Uçlar istemci anahtarı almıyor; koruma durum makinesindendir (ikinci çağrı 409). "Liste yalnız küçülebilir" ilkesine bu beş kayıt bilinçli bir istisnadır; kalıcı çözüm uçlara `X-Idempotency-Key` başlığı eklemektir.

# V12-GOV-008 karar kaydı

- Tarih: 2026-09-27
- Bağlam: `V12-ONL-009` ve `V12-TGO-002` başlamadan önce kod okunurken bulundu.

## Bulgu

- `src/Host/Experience/OnlineOrdering/YemeksepetiOrderIntakeService.cs` kurucuda
  `providers.Get(OnlineOrderProviders.Yemeksepeti)` ile tek platforma bağlanıyor.
- `src/Modules/OnlineOrdering/Yemeksepeti/OrderNormalization/YemeksepetiInboxProcessingStore.cs` yalnız
  `provider = 'yemeksepeti'` satırlarını işlemeye alıyor.
- Kutudaki ham yük `YemeksepetiWebhookInbox` erişim politikasıyla şifreleniyor ve yalnız onun `OpenPayload`
  yöntemiyle açılıyor. Şifreleme erişen bileşene bağlı değil (erişen yalnız anahtar çözümünü yetkilendiriyor),
  bu yüzden ortak bir kutu mevcut kayıtları aynı ana anahtarla açabilir.
- `V12-ONL-009` kalıcı imleç istiyor, sahipliğinde migration yolu yoktu.

## Karar

- Eksik görev `V12-ONL-010` açıldı: ortak gelen kutusu yazıcısı/açıcısı ve kayıtlı her platform için alım.
- `V12-ONL-009` ve `V12-TGO-002` `V12-ONL-010`'a bağlandı; `V12-ONL-009`'a migration ve kayıt yolları eklendi.

## Reddedilen alternatif

- Trendyol Go alımını `V12-TGO-002` içinde ayrı bir kopya servis olarak yazmak: stok, mutfak, iptal ve mutabakat
  akışı iki yerde çoğalırdı; `V12-ONL-007`'nin "akış bir kez yazılır" hedefine aykırı.

# V1-RMD-891 sonucu

Hesap kabul etmeyecek ikram/indirim artık hibe açılmadan 409 `DISCOUNT_BELOW_COLLECTED` alıyor; hibe (limit sayacı, hesap toplam sınırı, yönetici onayı) boşa harcanmıyor.

## Değişiklik
- `EnsureDiscountRequestableAsync` (hibeden önce çalışır) aday ikram/indirimi mevcut düzeltmelere ekleyip hesaplıyor; sonuç, hesaptan şimdiye kadar alınanın altındaysa hibe açmadan 409 veriyor. Eski, yalnız ikram için olan "tutarı aşıyor" ön kontrolü bu hesaplamaya katıldı (artık indirim de ön kontrolde 400 `DISCOUNT_EXCEEDS_PAYABLE` alır; eskiden hibe açılıyor, uygulamada reddediliyordu).
- Aynı anahtarla zaten uygulanmış isteğin tekrarı ön kontrollerden önce cevaplanıyor. Bu, mevcut bir kusuru da kapattı: kısmen kullanılmış ikramın (250 TL hesapta 140 TL) aynı anahtarla tekrarı, tutar kalan tutardan büyük göründüğü için 400 alıyordu (kırmızı test `ReplayingAPartlyUsedCompWithTheSameKeyReturnsTheAppliedResult`).
- Kilitli son kontrol (`ApplyDiscountAsync`) aynen duruyor; aday oluşturma ve tahsilat okuması iki yerde ortak (`CreateDiscount`, `ReadCollectedAsync`).

## Kanıt
- Kırmızı: red-1.log (4 yeni test kırmızı). Yeşil: green-1.log (BillingSplitHttpTests 73/73), final-billing.log (tüm Billing projesi).
- Mutasyon (mutations.txt): ön kontrol yok, `<=`, tekrar muafiyeti yok: öldü. "Mevcut düzeltmeleri yok say" mutantı ilk turda yaşadı; `AnAdjustmentThatOnlyTogetherWithAnEarlierOneUndercutsWhatWasCollectedSpendsNoApproval` eklendi, ikinci turda öldü. Her mutasyon cp+cmp ile geri alındı, her koşu yeniden derlendi.

## Açık sınır (bilinçli)
- Ön kontrol kilitsizdir: ön kontrolden sonra, hibeden önce gelen bir tahsilat yine de hibe açar, kilitli son kontrol 409 verir (çok dar yarış; hibe harcanmış kalır).
- Yönetici onayı verildikten sonra, kasiyer tekrar göndermeden önce ek tahsilat gelirse onaylı hibe kullanılamaz (409); yeni onay gerekir. Hibe iptali/yeniden kullanımı ürün kararıdır.

## İnceleme sonrası notlar
- Ön kontrolün ikramda eski "tutarı aşıyor" kuralından gevşediği iddiası yanlıştı: eski kural ikramı kalan ödenecek tutarla (bahşiş dahil) kıyaslıyordu; yeni hesaplama toplam indirimi taban tutar + ücretlerle kıyaslar. İkram kalan ödenecekten büyükse toplam indirim taban + ücreti de aşar; yeni kural eskinin red kümesini kapsar, yalnız indirim de artık ön kontrolden geçer. Hata kodu aynı (400 DISCOUNT_EXCEEDS_PAYABLE).
- Yönetici onayı bekleyen yolda tahsil edilenin altına inecek istek de hibe açmaz (`AnAdjustmentThatWouldWaitForAManagerButUndercutsWhatWasCollectedOpensNoRequest`).
- Ön kontrolün `_dataSource`/`_adjustments` null koruması yok: üretimde ikisi de her zaman kayıtlıdır; `ApplyDiscountAsync` koruması yapılandırmayı zaten yakalıyor, aynı sınıfın eski ön kontrolü de `_adjustments!` kullanıyordu.
- Tüm Billing projesi 291/291 (final-billing.log, 15 dk); eklenen son test ve yakın testler final-targeted.log (12/12).

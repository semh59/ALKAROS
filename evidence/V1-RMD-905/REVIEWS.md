# V1-RMD-905 incelemeleri

## Tur 1 (qa-uctan-uca-akis)
- Yüksek: sil-ekle bölünmüş hesabın tahsis ve düzeltmelerini FK cascade ile siler -> DÜZELTİLDİ (aynı id ve satır sürümüyle yerinde güncelleme, plan 6332259f, test + mutant 4).
- Orta: yarışta 500 -> hesap eşzamanlılığı BillConcurrencyException zaten 409 (ApiErrorCatalog); dar yarış, kabul.
- Orta: kısmi ödeme -> ÇÜRÜTÜLMEDİ, AÇIK (tur 4): açık hesaba tahsis yapılabiliyor ve ikram yolu yalnız hesap durumuna bakıyor; ikram sonrası ödenecek tutar tahsil edilenin altına inebilir. Ürün kararı (iade mi, ikramı reddetmek mi) bekliyor; RESULT.md Açık bölümünde.
- Düşük: yanlış yerdeki yorum -> DÜZELTİLDİ. Havuz bağlantısı ve çoklu hesap testi: kabul.

## Tur 2 (qa-backend)
- Orta: sabit tutarlı indirim > ikram sonrası taban -> hesap ödenemez -> DÜZELTİLDİ (409 DISCOUNT_EXCEEDS_PAYABLE, plan ea443559, test + mutant 5/6).
- Düşük: test 2 adı yanıltıcı -> yeniden adlandırılacak; kalem sırası değişimi kalıcı veriyi etkilemez.

## Tur 3 (qa-test-kalitesi): gerçek hata yok
- Test gücü notları: Reopened durumu testi eklendi (Theory Open/Reopened). Sınır değeri (indirim = kalan), birleştirilmiş hesap ve eşzamanlılık HTTP kodu testleri kabul edilen eksik; üretim davranışı kod okumasıyla doğrulandı (BillConcurrencyException 409 eşlemesi, kısmi ödemeli durumlar RequireItemChangeableAsync ile reddedilir).

## Tur 4 (qa-bulgu-dogrulayici): yeni hata yok
- Düzeltmeler kodla doğrulandı. Belge sapmaları (cascade ifadesi: migration 218 sonrası FK NO ACTION, eski şemada cascade; RESULT test sayısı; kısmi ödeme notu) düzeltildi.

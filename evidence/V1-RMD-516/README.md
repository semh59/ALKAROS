# V1-RMD-516 kanıtı

Sonuç: `GET /api/v1/terminals/{terminalId}/billing/bills/{billId}/receipt` bilgi fişi verisini (kalemler, ayarlamalar, toplamlar, ödemeler ve yöntemleri) tek yanıtta döndürür; veri yazmaz.

## Davranış
- Kasiyer oturumu gerekir (oturumsuz 401), olmayan hesap 404.
- Ödeme yöntemi türetilir: cari borç kaydı → Account, nakit satış hareketi → Cash, diğeri → Manual.
- Yalnız onaylanmış tutarı olan ödemeler, onay zamanına göre sıralı listelenir; indirim/hizmet/bahşiş ayarlamaları ve ayarlı ödenecek tutar dönülür.

## Kanıt
- `test-runs.log`: 3 gerçek Host + Postgres testi geçti.
- `mutations.log`: 13 mutant, hepsi killed (tur 1'de 2 hayatta kalan → test eklendi; ilk deneme sırasında test Postgres'i bellek baskısıyla kilitlendiği için o turun sonuçları atıldı, temiz turla yeniden koşuldu).

## Açık
- Yazdırma arayüzü V1-RMD-517'de.
- Mali fiş / yazarkasa değildir.

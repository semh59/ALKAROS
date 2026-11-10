# V1-RMD-900 sonucu

Cashier ödenmiş hesap ekranında "Fişi yazdır" artık onaylı: ilk tık "İlk basım orijinaldir, sonrakiler KOPYA yazar." uyarısını açar (düğme "Evet, yazdır" olur, yanına "Vazgeç" gelir) ve sunucuya gitmez; ikinci tık yazdırır; Vazgeç uyarıyı kapatır; sonraki basım yine sorar. Orijinal/kopya kararı sunucuda, değişmedi. React kasada fiş yazdırma yok (arama: yalnız split-payment.js), karşılığı gerekmiyor.

## Kanıt
- Testler: split-payment-receipt.test.js 17/17 (4 yeni: uyarı, onaydan sonra yazdırma ve tekrar sorma, Vazgeç, eski hata uyarıyla birlikte görünmez; 13 mevcut test iki tıkla uyarlandı). Kırmızı: red-1.log (uyarısız mutant düşüyor). StaticApps tamamı 40 dosya 436/436 (final-staticapps.log).
- Mutantlar (mutations.txt): 7 mutant öldü (uyarısız, onay yazdırmada sıfırlanmıyor, Vazgeç sıfırlamıyor, metin, etiket, Vazgeç düğmesi metni, eski hata uyarıda kalıyor); Vazgeç düğme metni mutantı ilk koşuda yaşadı (log tutulmadı), assert eklenip yeniden öldürüldü.

## Bilinen açıklar (kapsam dışı, not)
- Çift dokunma: hızlı iki tık uyarıyı atlayıp orijinali harcayabilir (testteki clickPrint de bunu yapar). Zarar: bir bilgi fişi KOPYA yazar; 901'deki yönetici sıfırlaması bunu düzeltir.
- Yanıt kaybı: sunucu orijinali kaydedip yanıt düşerse sonraki basım KOPYA olur; uyarı bunu bilmez. 901 ile aynı çözüm.
- Odak: render sonrası klavye odağı düğmeye geri verilmiyor.

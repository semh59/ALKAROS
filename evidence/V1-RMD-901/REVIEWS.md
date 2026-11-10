# V1-RMD-901 incelemeleri

## Tur 1 - qa-guvenlik-yetki
Yetki, kilit, gerekçe, denetim kaydı, hesap sahipliği sağlam. PO soruları (RESULT.md "Açık"): yönetici kendi bastığını sıfırlayabilir (kendi onayı ayarı yok), sıfırlama sayacı/sınırı yok, mevzuatta orijinal/kopya karşılığı. Orta/düşük: Paid kontrolü yok (basım yalnız Paid'de olabildiğinden bugün etkisiz), test boşlukları (başka terminal, eşzamanlı, 500 sınırı) not.

## Tur 2 - qa-frontend
Orta: yazılan gerekçe yeniden çizimde kayboluyordu (input dinleyicisi + test + mutant eklendi); sıfırlama ile yazdırma birbirini kilitlemiyordu (kilit + test + mutant eklendi); bölüm yetkisize görünüyor (kabul, sunucu 403). Düşük: erişilebilirlik notları.

## Doğrulama
Düzeltilen bulgular testli; kalanlar PO sorusu ya da bilinçli not. Yeni mutantlardan biri (sıfırlamada receiptBusy kontrolü) yedekli koruma çıktı (düğme zaten disabled), kod kaldırıldı.

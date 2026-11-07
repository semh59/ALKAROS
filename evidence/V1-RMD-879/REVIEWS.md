# V1-RMD-879 incelemeler

1. qa-backend: kritik/yüksek/orta bulgu yok. Düşük notlar: bekleyen iade reddedilince sonraki istek tutarı en çok 1 kuruş kayabilir (toplam kalem payını aşmaz); ödemeden sonra indirim eklenmesi mümkünse önceki iadeler eski tutardan hesaplanmış kalır (mevcut davranış, kapsam dışı).
2. qa-test-kalitesi: 4 orta bulgu (zayıf InRange, oran başına Tax/Net doğrulanmıyor, bahşiş terimi sınanmıyor, yüzde indirim/çoklu indirim yok). Düzeltildi: kesin kuruş değerli dağıtım ve KDV kırılımı testleri (ek ücretli, bahşişli), M8-M10 mutantları öldü. Yüzde indirim/çoklu indirim ve sıfır brüt kalem ayrı test edilmedi: kod yolu aynı toplamı kullanır (TotalDiscounts), açık risk düşük.
3. Doğrulama turu: domain testleri 25/25 (green3.done), host billing 78/78 (green.done, üretim kodu değişmedi).

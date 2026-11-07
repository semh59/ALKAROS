# V1-RMD-898 sonucu

Şüphe KISMEN gerçek. Toplam doğruydu (897 sonrası kalan adetler yeni indirimli paya göre iade edilir, iade toplamı tahsil edilene eşit: 100 + 20 + 30 = 150). Ama indirim iadeden sonra gelince geçerli bir iade reddediliyordu: 150 tahsilatlı, 250'lik hesapta ilk kalemin 1 adedi iade edilir (100), sonra 100 indirim gelir; kalemin ikinci adedinin payı 60, ama önceki iade fazla ödendiği için gerçekte ödenecek 20 iken aşım denetimi ham payı (60) ödemenin kalanıyla (50) karşılaştırıp REFUND_AMOUNT_EXCEEDS_PAYMENT veriyordu (red-1.log). Eski kod da aynı payı (60) reddederdi.

## Düzeltme
`PaymentRefunds.PriceItemsAsync` aşım denetimine yalnız ödemeye sığmadığı için kalan kuruş borcunu (negatif değil) verir: `Math.Max(carry, 0m)`. Negatif borç (indirimle fazla ödenmiş iade) toplamı düşürür ve denetimi şişirmez.

## Kanıt
- Kırmızı: red-1.log (ham borç mutantı: test düşüyor). Yeşil: green-2.log; PaymentRefundHttpTests final-refund.log; refundApi.test.ts 21/21; split-payment-cancel-order.test.js 10/10.
- Mutantlar (mutations.txt): 5 ayrı mutant öldü (ham borç = kıskaçsız hata, hata kodu seçimi iki biçimde, Cashier ve React sözlük girdisi).
- Testler: RefundAfterDiscountHttpTests.cs (2), refundApi.test.ts ve split-payment-cancel-order.test.js satırları.

## İkinci düzeltme (tur 1 bulgusu)
Önceki iade indirimle fazla ödenmişse, o fazla sonraki kalem iadelerinden düşer. Kalan kalem fazlayı karşılamıyorsa (ör. 150 tahsilat, 100 iade, 100 indirim, sonra yalnız 30'luk kalem: 60+30-100 = -10) istek "tutar sıfırdan büyük olmalı" yerine REFUND_EARLIER_OVERPAID (400) ile "Önceki iade sonradan verilen indirim yüzünden fazla ödendi; bu kalemi diğer kalan kalemlerle birlikte iade edin." mesajını alır; aynı kalem kalan kalemlerle birlikte iade edilince tutar 50 (= ödemenin kalanı) olur. Mesaj React kasa (refundApi.ts) ve Cashier (split-payment.js) sözlüğünde Türkçe.

## Açık (para kararı, değiştirilmedi)
Fazla ödenen iade tutarı müşteriden geri istenmez ve sonraki iadelerden düşülür; alternatifler: kaybetmek ya da kalem iadesini bloke etmemek. Bu işletme kuralıdır, ürün sahibinin kararı gerekir. Çalışan kalemleri birlikte iade ederek ilerleyebilir. Kalemsiz tutar iadesi çıkış yolu DEĞİLDİR: ödemenin tüm kalanını öder ve kalem adetlerini düşmez; bu yüzden mesaj onu önermez.

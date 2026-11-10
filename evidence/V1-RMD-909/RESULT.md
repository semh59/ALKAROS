# V1-RMD-909 sonucu

Kalem iadesi yapılmış hesaba hesap düzeyinde indirim ya da ikram artık uygulanmıyor (Semih kararı: fazla iade oluşmasın): 409 DISCOUNT_AFTER_REFUND, "Bu hesapta kalem iadesi yapılmış; iadeden sonra indirim ya da ikram uygulanamaz." Önceden kalem iade edilip sonra indirim verilince iade edilen kalemin payı düşüyor, müşteriye fazla ödeniyordu; fark ancak sonraki iadelerden mahsup edilebiliyordu.

## Değişiklik
- BillingSplitStore: kontrol hem yöneticiye gitmeden önceki ön kontrolde (onay harcanmaz) hem indirimi yazan işlemde (hesap-ödeme kilidi, hesap satırı, tahsis kilidi sırasıyla) yapılır; sayılan iadeler Requested, AwaitingManualRefund, Completed kalem iadeleri.
- Hata sözlüğü Türkçe 409.

## Kanıt
- Kırmızı (red-1.log): iade sonrası indirim ve ikram 200 dönüyordu.
- Yeşil: RefundThenDiscountHttpTests ve komşu indirim testleri 7/7 son kodla (green-final.log), tüm Billing.Tests 319/319 (billing-full.log; koşu, istisna yeri ve Rejected testi eklenmeden hemen önceki kodla yapıldı, ikisi green-final.log ile doğrulandı).
- Mutantlar (mutations.txt): 3 öldü, 1 hayatta (kilitli işlemdeki ikinci kontrol; ön kontrol tek iş parçacıklı testte aynı sonucu verir, kabul).

## Açık
- Eski verideki fazla iadeler ve REFUND_EARLIER_OVERPAID mahsup mantığı (898) korunuyor, düzeltilmiyor.
- İndirim yolu tahsilatı iadeleri düşmeden sayıyor (906'daki fark); ayrı karar.

## Kararlar ve notlar
- Bekleyen (Requested) iade isteği varken indirim/ikram engelli kalır; sona erme yolu eklenmez, yönetici onaylar ya da reddeder (Semih kararı).
- Kalem düzeyi ikram (CompBillLineAsync) iade sonrası açık kalır: iade edilmiş kalemin ikramı ek kayıp üretmez, diğer kalemlerin payını değiştirmez.
- İadeden önce uygulanmış indirim kalır; iade o indirimli değerle fiyatlanır (tutarlı).
- Bahşiş ve indirim geri alma, hesap-ödeme kilidini almıyor (önceden de böyleydi); fazla iade üretmezler.

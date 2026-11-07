# V1-RMD-884 incelemeleri

## qa-backend (kritik/yüksek yok)
- Doğrulandı: toplam ve KDV tüm kalemler dağıtılınca tutuyor (kısmi miktar, çok sahipli kalem, yüzde + tutar indirimi, kırpma), ReplaceAsync doğrulamasıyla aynı formül, eski imza geriye uyumlu.
- Kabul edildi (RESULT.md): sıfıra düşen kalem payında genel 400; kayıttan sonra eklenen indirimin tasarımı bayatlatması (tüm modlarda önceden var); ekran uyarısı indirim+bahşiş birleşiminde görünmeyebilir (sunucu 409 Türkçe açıklar); kalem bazlı indirim alanı hesaplayıcıda okunmuyor (879 davranışı).

## qa-test-kalitesi (kritik/yüksek yok)
- Düzeltildi: yalnız bahşiş hesabında ürün bölme 409 testi (A7: iç `||` -> `&&` mutantı öldü); yüzde + tutar indirimi birlikte domain testi. Sıfır-net dalındaki `adjustment = null` satırı mevcut `AZeroNetAdjustment...` testiyle korunuyor (satır silinirse toplam tutmaz, kayıt reddedilir).
- Kabul edildi: ekran uyarısı sunucu bayrağına bağlanmadı (DTO değişikliği); sahip başına tutar HTTP asserti; indirimli hesapta eşit bölme senaryosu (eşit bölme kodu değişmedi).

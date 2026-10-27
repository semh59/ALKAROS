# V1-RMD-731 - Değiştiricili ikramda 036 CHECK uyumsuzluğu (603 B004)

**Sonuç:** Fiyatlı değiştiricisi (ör. fazladan peynir) olan ürün artık ikram edilebiliyor ve hesap satırı yazılabiliyor. Önceden veritabanı kuralı yalnız adet x birim fiyatı kabul ettiği için istek 400 ile reddediliyordu.

## Kök neden ve düzeltme
İkramın indirimi satır toplamının tamamı (değiştirici dahil, `OrderItem.LineSubtotalValue`: 115); `ck_order_items_nonnegative_amounts` ve `ck_bill_items_nonnegative_amounts` ise `discount_amount <= round(adet x birim fiyat)` (100) şartını arıyordu. Değiştirici fiyatları başka tabloda olduğundan CHECK bunu kapsayamaz. Migration 208 iki kuraldan yalnız bu üst sınırı kaldırır (negatiflik ve brüt = net + KDV kalır); üst sınır alan kodunda (`OrderItem`, `BillItem` kurucuları) zaten korunuyor. Geri alma, eski kuralı `NOT VALID` ekler (değiştiricili ikram satırları geri almayı engellemesin).

## Kanıt (logs/)
- Kırmızı → yeşil: `AnItemWithAPricedModifierCanBeCompedAndItsBillLineIsWritten` önce 400 verdi (`logs/red.log`), migration 208 ile geçti (`logs/green.log`). Test, sahte veri değil gerçek PostgreSQL ve gerçek 036 kuralıyla koşuyor; Comp test projesi önceden 036'yı uygulamıyordu, bağlantısı eklendi.
- Comp Host testleri 21/21; Manifest 18/18; Migrations 16/16.
- Mutasyon: kuralın üst sınırını geri koyan iki mutant (sipariş ve hesap satırı) KILLED, dosya geri yüklendi.

## Açık
Veritabanı artık değiştiricili satırda indirim üst sınırını kendisi denetlemiyor; sınır yalnız alan kodunda. İkram dışı indirim satırlarında (satır indirimi) bu fark sonucu veritabanı seviyesinde tutarsız tutar yazan bir koda karşı tek savunma kodun kendisi.

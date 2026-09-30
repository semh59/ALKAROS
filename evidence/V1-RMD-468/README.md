# V1-RMD-468 - KDV dahil satır fiyatı: Host, raporlama ve çift ekran

`V1-RMD-467` satır hesabını KDV dahil yaptı; bu görev onu varsayan iki yeri uyumlar.

## Değişiklikler

- `ChannelReportService`: kabul edilen net tutar `subtotal - discount_total` yerine `total - tax_total` (ikisi de: gün satırları ve defter toplamı). Artık `subtotal - discount_total` brüttür.
- `DualScreenStore.Display`: ikram satırının brütü `birim x adet` (üste `x (1 + oran)` eklenmez); kullanılmayan `tax_rate` sütunu sorgudan çıkarıldı.
- Test tohumları: kanal raporu tohumu artık KDV dahil biçimde yazar (`total = subtotal - discount`, KDV toplamın içinde); ham SQL tohum açıklamaları düzeltildi.

## Kanıt

- Yeni test `AComplimentaryLineIsShownAtItsTaxInclusivePriceNotWithTaxAddedOnTop` (4 adet x 1 TL ikram satırı %10 KDV ile 4,00 gösterilir): `green-dualscreen-store.log` (15/15 yeşil).
- Kanal raporu altın veri seti KDV dahil tohumla aynı beklentilerle yeşil (`green-Modules.Reporting.Channels.log`, 8/8); ham tohum yorumu değişen Comp ve VoidSent projeleri yeşil.
- Mutasyonlar (`mut/`): kanal raporu sorgusunu eski `subtotal - discount_total` yapmak 4/8 testi kırar; çift ekranda `x 1,1` eklemek yeni testi kırar (beklenen 4,00, gerçek 4,40). Dosyalar geri alındı, `cmp` özdeş.
- Gerçek Host (`real-host-trial.live.log`): gerçek sipariş toplayıcısıyla üretilmiş 100 TL / %10 KDV'li sipariş online kanalda tamamlanmış sayıldığında kanal raporu `acceptedValue 100, acceptedNetValue 90.91, acceptedTaxValue 9.09`, defter kontrolü dengeli.

## Açık kalan

- İstemci etiketleri ve kasa ekranı fiyatı `V1-RMD-469`.

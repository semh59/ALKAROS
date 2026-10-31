# V1-RMD-768 sonuç

Bulgular V1-KIT-003 ve V1-KIT-004: iş gönderimden önce `Printing` olarak kalıcılaşır; çalışan çökerse süresi dolmuş `Printing` işi `RecoverExpiredLeasesAsync` tarafından körlemesine `Pending` yapılır ve bilet yeniden basılırdı. Aynı sınıfın belirsiz-çıktı yolu (`PrinterTransmissionUncertainException`) çift basımı önlemek için işi `AwaitingOperatorReview`'a koyar.

Düzeltme: kurtarma ikiye ayrıldı. Süresi dolmuş `Leased` (hiç gönderilmemiş) `Pending`'e döner; süresi dolmuş `Printing` `AwaitingOperatorReview` olur ve nedeni (`last_error`) yazılır. `PrintJob.ResetExpiredLease` aynı kuralı uygular.

## Kanıt
- Kırmızı (`red/`): iki yeni test düştü (birim ve gerçek PostgreSQL), 39 geçti.
- Yeşil (`green/`): `PrintQueue` test projesi 41/41, exit 0 (mevcut `Leased` → `Pending` testleri dahil).
- Mutasyon: SQL baseline'a geri alınınca (`mutation-sql-reverted/`) yalnız PostgreSQL testi, alan nesnesi baseline'a geri alınınca (`mutation-domain-reverted/`) yalnız birim testi düştü. Kaynaklar `cmp` ile geri yüklendi.

## Sınırlar
- V1-KIT-004'ün "gönderimden önce kalıcı işaret" kısmı iş düzeyinde zaten `Printing` kaydıyla vardı; bu görev o işareti kurtarma kararına bağlar. `physical_print_deliveries` içinde InFlight satırı açılmadı, bu yüzden çökmeyle `AwaitingOperatorReview`'a düşen işin karşılığında "Unknown" teslimat kaydı oluşmaz.
- Operatör onay ekranında yeniden yazdırma eylemi yoktur (arayüz bunu zaten belirtir); iş yalnız görünür ve otomatik yeniden basılmaz.
- Kurtarma süresi (kiralama süresi) bir yazdırma uzun sürerse süresi dolmuş sayabilir; süre değeri bu görevde ele alınmadı.

# V1-RMD-768 sonuç

Bulgular V1-KIT-003 ve V1-KIT-004: iş gönderimden önce `Printing` olarak kalıcılaşır; çalışan çökerse süresi dolmuş `Printing` işi `RecoverExpiredLeasesAsync` tarafından körlemesine `Pending` yapılır ve bilet yeniden basılırdı. Aynı sınıfın belirsiz-çıktı yolu işi `AwaitingOperatorReview`'a koyar.

İlk düzeltme (e41bce26) işi yalnız `AwaitingOperatorReview`'a koydu. Kod incelemesi bunun yetmediğini yakaladı: operatör listesi ve yeniden basım onayı `physical_print_deliveries` içindeki `Unknown` kayda bağlıdır; kayıt yoksa iş ekranda görünmez ve yeniden basılamaz, yani çift basım riski kalkıp "bilet hiç basılmadı" riski artardı. Bu yüzden plan kapsamı genişletildi (iki ara baseline) ve kurtarma aynı komutta `Unknown` teslimat kaydı da yazar.

Düzeltme (son hâl): süresi dolmuş `Leased` → `Pending`; süresi dolmuş `Printing` → `AwaitingOperatorReview` + aynı komutta işe bağlı `Unknown` teslimat (açık teslimatı olan iş için yenisi yazılmaz). `PrintJob.ResetExpiredLease` aynı kuralı uygular. Test veritabanına teslimat tablosunun migration'ları (018, 034, 037) eklendi.

## Kanıt
- `red/` ve `red2/`: ilk iki ve sonraki iki test kırmızı (birim ve gerçek PostgreSQL; `red2`: operatör listesinde görünmüyor).
- `green2b/`: `PrintQueue` test projesi 43/43, exit 0. `green2/` teslimat tablosu test veritabanında olmadığı için düşen ara koşudur (4 başarısız); kanıt sayılmaz, tabloyu ekleyen `green2b/` esas alınır. `host-kitchen-regression/`: Host KitchenOperations paketi 73/73 (üretim şemasıyla).
- Mutasyon: `mutation-sql-reverted/` ve `mutation-domain-reverted/` (ilk sürüm, birim ve PostgreSQL testi); `mutation-no-delivery-record/` (teslimat kaydı yazımı kaldırılınca iki yeni test düştü). Kaynaklar `cmp` ile geri yüklendi.

## İnceleme
- Test incelemesi (ilk sürüm): engelleyici bulgu yok; düşük: "sonraki işçi yeniden almaz" kontrolü zayıf, karışık durum testi yoktu (eklendi).
- Kod/ürün incelemesi (ilk sürüm): CHECK kısıtı ve alanlar temiz; ürün riski bulundu (yukarıda) ve giderildi.
- Son sürüm için ikinci tur inceleme: aşağıda.

## Sınırlar
- Operatör onay ekranında yeniden yazdırma eylemi mevcut akıştır (`/deliveries/{id}/reprint-approval`); bu görev yeni eylem eklemez.
- `NOT EXISTS` korumasının (açık teslimatı olan işe ikinci kayıt yazmama) doğrudan testi yoktur.
- Kiralama süresi (30 sn) yazdırma taşıma süresinden (yaklaşık 15 sn) uzundur; çok örnekli dağıtımda başka bir işçinin hâlâ yazdıran işi yanlışlıkla incelemeye düşebilir (kodla doğrulanmadı).
- Kısmi toplu kiralamada `MarkPrinting` süreyi uzatmaz (önceden de böyleydi); bu durumdaki `Leased` iş yine `Pending` olur.

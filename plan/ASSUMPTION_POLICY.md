# Evidence and Assumption Policy

## Yasaklar

- PDF'de, resmi sağlayıcı sözleşmesinde veya onaylı iş kararında bulunmayan bir
  API, status, alan, mevzuat sonucu ya da kullanıcı davranışı gerçekmiş gibi
  yazılmaz.
- Dış sistem erişimi olmadan adapter görevi `Done` yapılmaz.
- Belirsiz iş kuralı geliştirici tercihiyle kapatılmaz.
- Örnek veri, provider cevabı veya test sonucu gerçek kanıt gibi sunulmaz.

## Kanıt sınıfları

1. `PDF baseline`: Master PDF'de açıkça yazılı gereksinim.
2. `Correction`: PDF denetiminde kanıtlanan çelişki veya eksik.
3. `External evidence`: Resmi mevzuat, sağlayıcı dokümanı, sandbox veya cihaz
   transkripti.
4. `Business decision`: Ürün sahibi/operasyon/muhasebe tarafından tarihli onay.
5. `Product decision`: Ürün sahibinin (Semih) doğrudan, PDF veya dış kaynak
   gerektirmeyen mühendislik/ürün kararı. Source basis kimliği: `PO:<tarih>`.
   PDF veya external kaynak yoksa/yetersizse bu sınıf geçerlidir; ayrı bir
   revalidation task açmaya gerek yoktur.

## Source basis kimlikleri

- `PDF:<section>`: `PDF_SOURCE.md` ile sabitlenen PDF bölümü.
- `CORR:<C-number>`: `TRACEABILITY.md` içindeki doğrulanmış düzeltme veya açık.
- `EXT:<source-id>`: `OFFICIAL_SOURCE_REGISTER.md` içindeki resmî kaynak.
- `DEC:<task-id>`: Tamamlanmış ve tarihli karar görevi.
- `PO:<date>`: Semih'in doğrudan onayı, ayrı task gerekmez.

Serbest metin kaynak adı, kaynaksız “best practice” veya yalnızca genel PDF adı
geçerli `Source basis` değildir.

## Bilinmeyenlerin yönetimi

Bir davranış yukarıdaki kaynaklardan türetilemiyorsa:

- Görev `Blocked` kalır.
- Ayrı bir decision/validation task açılır.
- Karar çıktısı source, date, approver ve rejected alternatives içerir.
- Tüketici implementation görevleri bu task'a dependency verir.
- Eksik özel contract, credential, sandbox veya cihaz kanıtı `Blocker` alanında
  açıkça yazılır.

## Dil

Task ve kod kimlikleri English; plan açıklamaları Turkish; provider'ın resmi
alan/status adları değiştirilmeden contract evidence içinde saklanır.

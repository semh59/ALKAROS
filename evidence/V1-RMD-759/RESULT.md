# V1-RMD-759 sonuç

Bulgu (V1-RMD-750 denetimi): sweeper tek `LIMIT 200` ile `updated_at DESC` sıralıyordu; 750 yalnız yönü değiştirmişti. 200'den fazla yeni kapanamayan hesap varsa eski kapanabilir hesaplar aç kalıyordu.

Düzeltme: adaylar `bill_id` anahtar sayfalamasıyla (sayfa başına 200) bitene kadar gezilir; her aday her taramada bir kez denenir.

## Kanıt
- Kırmızı (`red/`): kapanabilir hesap en eski, üstüne 201 kapanamayan: 1 failed, 2 passed (`closed` 1 beklenirken farklı).
- Yeşil (`green/`): sweeper testleri (750, 736 ve iki yeni test) 4/4 geçti, exit 0. Tam `PaymentTender` paketi (`green-full/`): 59/59 geçti, exit 0.
- Mutasyon (`mutation-single-page/`, `mutation-single-page-v2/`): sayfalama tek sayfaya indirilince
  - ilk test (kapanabilir hesap en eski) mutantı ÖLDÜRMEDİ: hesabın id'si rastgele olduğu için ilk sayfaya düşüyordu, test yalnız eski sıralama hatasını yakalıyordu.
  - bu yüzden ikinci test eklendi (en büyük id'li hesap kapanabilir; ikinci sayfaya düşer). Mutant `v2` koşusunda bu testle öldü (1 failed). Kaynak `cmp` ile geri yüklendi.
- `green-precheck/`: ikinci testin ilk denemesi, `max(uuid)` Postgres'te yok hatasıyla düştü; sorgu `ORDER BY bill_id DESC LIMIT 1` yapıldı.

## Sınırlar
- İkinci test kırmızıyı eski kodda kanıtlamaz (eski kodda konum `updated_at`'e bağlı); yalnız sayfalama mutantını öldürdüğü gösterildi.
- Tarama artık tüm açık ve ödemeli hesapları gezer; çok büyük aday sayısında tarama süresi uzar, aralık ve süre sınırı bu görevde değişmedi.

## İnceleme
- Kod incelemesi ve test incelemesi, engelleyici bulgu yok. Orta not: tarama artık sınırsız; `payment_allocations(bill_id)` indeksi doğrulanmadı.
- Mutantın tam diff'i kaydedilmedi; tarifi: `if (candidates.Count == 0)` koşuluna `|| after != Guid.Empty` eklemek.

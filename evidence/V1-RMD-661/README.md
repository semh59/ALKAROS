# V1-RMD-661 — Olay kuyruğu dayanıklılığı

Kapatılan bulgular: 596-B003, B005, B006, B008, B009, B017, B018.

## Sonuç
- Tüketici çağrısı zaman aşımına uğrarsa (30 sn üst süre) başarısız deneme sayılır; parti takılmaz (B003, B017).
- Süresi dolan kira bırakılırken deneme sayacı artar, bütçe dolarsa mesaj ölür; zehirli mesaj sonsuza kadar dönmez (B008).
- Kapatma sırasında teslim edilmiş mesaj `dispatched` yazılır; işlenmemiş mesajlar 5 dk beklemeden `pending` olur; her mesajdan önce kira yenilenir, kaybolmuşsa atlanır (B009).
- `last_error` artık istisna türü adını içerir ("handler failure: TypeName"), metni asla; ayrıca Host günlüğüne uyarı yazılır (B006).
- Teslim edilmiş kayıtlar 14 gün, ölü kayıtlar 90 gün sonra saatlik temizlikle silinir (B005).
- Veritabanı kesintisinde dispatcher bekleme süresi 1 sn'den 30 sn'ye üstel artar (B018).

## Kanıt
- test-runs.log: Messaging 57/57 (15 yeni test), temiz derleme 0 uyarı 0 hata.
- dep-*.log: Kitchen OrderItemStateSync 7/7, OnlineOrdering Host 179/179, SecurityAdministration Host 30/30, Mimari ModuleBoundaries 10/10.
- mutations.log: 14 mutant, 14 öldü. İlk tam koşuda 12 "hayatta" görünen mutantın nedeni, takılan bir testin dll'i kilitleyip sonraki derlemeleri bozmasıydı; betik her mutant sonrası kilitli test sürecini kapatacak şekilde düzeltildi. İki gerçek zayıf test (kira yenileme, temizlik) güçlendirildi ve yeniden koşuldu; log'daki eski SURVIVED satırları bu geçmişi gösterir, son satırlar KILLED.

## Doğrulanamadı
- `OutboxDispatcherHostedService` içindeki bekleme süresi ve saatlik temizlik çağrısının bağlantısı için ayrı test yok (Host'ta bu hizmet için test projesi yok); mantığın kendisi `OutboxDispatchPacing` ve `PurgeAsync` testleriyle kanıtlı, hizmet yalnız bunları çağırır.

## Sonraki göreve ayrılanlar
B007 (profilsiz olay bütçesi), B016 (açılıştaki ölüler için izleyici), B021 (sıra sözleşmesi), B024 (ölü inbox şeması).

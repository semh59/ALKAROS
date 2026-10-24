# V1-RMD-714 - Masa veri kısıtları, test boşlukları, masa tutarı ve gönderilmiş hesap

Kapanan V1-RMD-601 bulguları: B023, B027, B029, B030.

## Değişiklikler
- Migration 205: masa başına en fazla bir aktif rezervasyon ve katılımcı masa başına en fazla bir aktif birleşim satırı (kısmi benzersiz indeks); kişi sayısı ≥ 1, bitiş zamanı başlangıçtan sonra, masa kendisiyle birleşemez (CHECK); `table_sent_checks` tablosu.
- Kasaya gönderilen hesap `table_sent_checks`'e yazılır, geri alınınca (tekrar geri almada da) silinir. Transfer, birleştirme ve ayırma bu hesapları ve hesaplarının ödemelerini taşımaz, saymaz ve işaretçi seçmez (B030).
- Masa tutarı masadaki tüm açık siparişleri toplar (kapalı ve gönderilmiş olanlar hariç); birleşik grubun her masası aynı toplamı gösterir (B029).
- Yeni testler: birleştirme ve transfer yarışı (`Task.WhenAll`, tam bir kazanan), rezervasyon yarışı, veritabanı kısıtları (23505, 23514), gönderilmiş hesap davranışı, masa tutarı, gönderme ve geri alma kaydı (B027). Boş masa, siparişsiz, kısmen ödenmiş, çok siparişli ve tekrar teslim senaryoları V1-RMD-710 ve 711 testlerinde zaten vardı.

## Test kanıtı (gerçek PostgreSQL, tümü exit 0)
TableMerge 42, TableTransfer 40, Reservations 33, CurrentPointers, Host Tables 30, manifest 18, ModuleBoundaries. Günlükler `logs/`. Migration 205 ileri/ikinci ileri/geri: `logs/migration-up-down.log`.

## Mutasyon (`mutate.mjs`, `mutations.log`)
19 mutantın 19'u KILLED, kaynaklar geri yüklendi ve doğrulandı. İlk koşuda R2, H2, H3, T3 SURVIVED; test eklenip yeniden koşuldu (H2 betiği de yanlış yeri değiştiriyordu, düzeltildi). Bellek baskısı ikinci partiyi öldürdü; kaynakta kalan T3 mutantı elle geri alınıp doğrulandı.

## Doğrulanamayan / açık kalan
- Mevcut canlı verilerde daha önce gönderilmiş hesaplar için geriye dönük kayıt yok (geri doldurma yapılmadı: sipariş durumundan güvenilir çıkarılamıyor); yalnız bu sürümden sonra gönderilenler korunur.
- Birleşimde ilk aktif rezervasyon indeksi, eski veride çift aktif satır varsa migration'ı reddeder; böyle veri bu depoda yok.
- Sunucudaki dağıtım adımı (migration'ın canlıya uygulanması) bu görevde yapılmadı.
- B025, B026, B031, B033, B020 sunucu kısmı: V1-RMD-716; B032 ve S4–S7: V1-RMD-715.

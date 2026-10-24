# V1-RMD-712 - Rezervasyon ve masa sunucu kuralları

Kapanan V1-RMD-601 bulguları: B013, B014, B015, B016.

## Değişiklikler
- Rezervasyon, rezerve alma, iptal, süre dolumu, transfer, birleştirme ve ayırma zamanları sunucu saatiyle yazılır; istemcinin gönderdiği zaman yok sayılır.
- Rezervasyona verilen sipariş, o masaya ait olmalıdır (oluşturma ve rezerve alma); aksi halde 400.
- Bitiş zamanı geçmişte olan rezervasyon oluşturulamaz (400).
- Elle süre dolumu yalnız bitiş zamanı gelmiş rezervasyonda çalışır (`RESERVATION_NOT_DUE`, 409). Bitiş zamanı olmayan rezervasyon hiç otomatik sona ermez.
- `ReservationExpiryHostedService` 60 sn'de bir süresi dolan aktif rezervasyonları sona erdirir ve masayı boşaltır (`ExpireDueAsync`).
- Dolu masaya `Reserve` komutu sunulmaz.
- KASA-1 teknik masası masa listesinde görünmez.

## Test kanıtı (gerçek PostgreSQL, tümü exit 0)
Reservations 31 (4 yeni), Host Tables 28 (6 yeni: TableServerRulesHttpTests, kayıt testi genişledi), ModuleBoundaries. Günlükler `logs/`.

## Mutasyon (`mutate.mjs`, `mutations.log`)
S1–S16 hepsi KILLED, kaynaklar geri yüklendi ve doğrulandı. Bellek baskısı nedeniyle ilk koşu S1'den sonra durduruldu, kalan mutantlar üç partide koşuldu; yarım kalan S2 mutantı elle geri alınıp doğrulandı ve yeniden koşuldu.

## Doğrulanamayan / açık kalan
- Rezervasyona misafir adı, telefonu ve saati: kişisel veri ve ürün kararı bekliyor (kapsam dışı).
- Süre dolumu döngüsünün 60 sn zamanlayıcısı test edilmedi; süpürme mantığı (`ExpireDueAsync`) ve kaydı test edildi.
- Bitiş saati girişi istemci ekranında yok (V1-RMD-713).

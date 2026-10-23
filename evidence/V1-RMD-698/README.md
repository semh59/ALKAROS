# V1-RMD-698 - Göç satır sonları, indeksler ve şema tutarlılığı

## Ne değişti

- **Satır sonu politikası (B047):** `.gitattributes` `database/migrations/**` için `-text` koyuyor; git hiçbir göç dosyasının satır sonunu yeniden yazmıyor. Sağlama toplamı dosyanın ham baytlarının SHA-256'sı olduğu için, Windows satır sonlu 14 eski dosya (dağıtılmış sağlama toplamları geçerli kalsın diye) dondurulmuş liste olarak testte duruyor; listedeki her dosya hâlâ CRLF içermeli, listede olmayan hiçbir dosya `\r` içeremez.
- **Göç 201:**
  - Ekran koruyucu tablosuna tür (png, jpeg, webp, mp4) ve boyut (1 bayt - 20 MB) kısıtı; ihlal eden satırlar önce silinir.
  - `row_version` INTEGER'dan BIGINT'e (diğer tüm ekran tabloları gibi).
  - `pairing_requests` ve `display_sessions` için `expires_at > created_at` kısıtı; ihlal eden satırlar önce silinir.
  - `terminals(active_order_id)` için kısmi indeks; makbuz sorgusu için `account_transactions(source_reference_id, source_reference_type)` indeksi.
  - İkisi de `IF NOT EXISTS`. Geri alma betiği hepsini geri alıyor.
- **Koşullu istek:** GET önce yalnız `updated_at` okuyup ETag'i karşılaştırıyor; eşleşirse içerik (en çok 20 MB) hiç okunmuyor.
- **Türkçe sıralama:** Ekran katalogu `tr-TR-x-icu` ile sıralanıyor; imleç karşılaştırması da aynı harmanlamayı kullanıyor.
- **Eksik kanıt klasörleri:** 096, 100, 101, 119 göçleri için ileri-geri-ileri testi (kendi sürümüne kadar kırpılmış sıra ve dosya kopyası üzerinde).
- Kayıt: `order.json`, `PhaseBMax=201`, `ManifestTests`, `docs/data/migration-rehearsal-profile.md`.

## Kanıt

- `mutate.mjs` / `mutations.log`: 17 mutant, sonuçlar `mutations.log` içinde; her mutantın ardından kaynak geri yüklenip doğrulandı.
- Yeni testler: `MigrationFileAndSchemaTests` (satır sonu, kısıtlar, indeksler, 201 geri alma, 096/100/101/119 ileri-geri), ekran koruyucu ETag testleri (304, değişen etiket, içerik sütunu yetkisiz rolde okunmadan ETag), katalog Türkçe sıralama testi.
- Tam `Host.Tests` (275 test) ilk koşuda 2 kırmızı verdi: süresi dolmuş eşleştirme taklit eden iki eski test oluşturma zamanını geri çekmeden süreyi geçmişe alıyordu (yeni kısıtı doğru biçimde ihlal ediyordu); testler düzeltildi, ilgili sınıflar 55/55 yeşil. Mimari projeler yeşil. Kayıtlar `logs/` altında.

## Yapılmayanlar / doğrulanamadı

- **304 yolunda içeriğin hiç okunmadığı** HTTP düzeyinde ölçülemedi (davranış aynı); yalnız ETag sorgusunun içerik sütununa erişmediği, içerik yetkisiz bir rolle doğrulandı.
- **14 eski dosyanın satır sonu** bilerek değiştirilmedi (dağıtılmış sağlama toplamı).
- **Büyük veride 201 süresi:** Large/Stress sınıflarında ölçülmedi; V20-DRL-001'in işi.

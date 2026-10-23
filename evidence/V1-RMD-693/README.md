# V1-RMD-693 - Hata kataloğu ve zarf

## Ne değişti

- **Veritabanı erişilemiyor (B021):** bağlantı kopması/reddi, sunucunun açılması-kapanması, kaynak tükenmesi (SQLSTATE 08, 53, 57P0, 58) ve Npgsql bağlantı hataları `503 DATABASE_UNAVAILABLE` + `Retry-After: 5` dönüyor. Veritabanının çalıştırıp reddettiği sorgu (kısıt, kötü değer, hata) artık 503 değil `500`. `/health/ready` de aynı yoldan 503 veriyor.
- **Genel `InvalidOperationException` artık 409 değil (B022):** katalog için `CatalogRecordNotFoundException` (404 `NOT_FOUND`) ve `CatalogConcurrencyException` (409), rol yönetimi için `RoleManagementConflictException` (409) eklendi; ilgili 4+5+11 fırlatma noktası bu tiplere geçti. "Kayıtlar çok fazla" gibi gerçek hatalar düz kalıyor (500).
- **Gövdesiz yanıt (B084, B083):** `/api` altında gövdesiz 401/403/404/405/415/429/5xx standart zarfla tamamlanıyor. Bilinen adrese yanlış metotla gelen istek `405 METHOD_NOT_ALLOWED` + `Allow`; router'ın kabul ettiği metot 405 diye raporlanmıyor.
- **Sipariş satırı (B092):** bilinmeyen satır 404, artık Taslak olmayan satır 409 (`DualScreenConflictException`).
- **İngilizce sızıntı ve biçim (B089, B096):** ödeme hatalarında durum ve yöntem adları Türkçe (`PaymentStatusText`, `TenderMethodText`), tutarlar `tr-TR` (`40,00 ₺`).
- **Belge (B132, B134):** `docs/architecture/api-contract-standard.md` kataloğun kodda olduğunu, 503/500 ayrımını, gövdesiz yanıt kuralını ve iki zarf biçimini anlatıyor; var olmayan `error-code-catalogue.md` atfı ve yanlış `ALLOCATION_EXCEEDS_PAYABLE` 422 kodu (gerçek: `TENDER_OVER_ALLOCATION` 409) düzeltildi.
- **Katalog testi (B024):** `ApiErrorCatalogTests` yeni tipleri, SQLSTATE 08/53/58/XX000/22003 satırlarını, `LAST_ROLE_MANAGER` satırını, durum-kod tablosunu ve zarf uç testini kapsıyor.

## Kanıt

- `mutate.mjs` / `mutations.log`: 33 mutant. İlk geçişte E6 (SQLSTATE 58), E8 (LastRoleManager yakalama), E9 (405 kodu), P3 (durum adı), P5 (ham durum), I1/I2 (depo çakışma tipleri) sağ kaldı; testler güçlendirildi, ikinci geçişte hepsi KILLED. Kaynak her seferinde geri yüklendi.
- Testler: `Host.Errors.Tests`, `Host.Tests` (tam), `PaymentTender`, katalog modülü, yetki modülü, mimari testler (`logs/`).
- Çalışırken bulunan hata: yeni 405 mantığı, router'ın 415 ile reddettiği doğru metotlu isteği "405" diye raporlayacaktı; metot izinliyse 404 kalıyor, testle kilitli.

## Yapılmayanlar / doğrulanamadı

- **B065 (istemci sözlüğü):** eklenmedi. Gösterilen metin zaten sunucu kataloğundan Türkçe geliyor; istemcide ikinci sözlük kopya olurdu. Tek sözlük `ApiErrorCatalog`.
- **Yerel eşleyiciler:** yaklaşık 20 uç noktadaki yerel `PostgresException or NpgsqlException => 503` anahtarları (ör. `BillingSplitApplication`) kataloğun dışında ve bu görevde değiştirilmedi; orada her Postgres hatası hâlâ 503. Kısa `{code,message}` zarfı da sürüyor (B134, yalnız belgelendi).
- **B085:** iki manuel çözüm kodu bağlama doğru mesajlar veriyor (doğrulayıcı); değişiklik yapılmadı.
- **Postgres zaman aşımı:** `TimeoutException` tek başına (Npgsql sarmalaması olmadan) 500 kalıyor.
- İstemci tarafı (PosTerminal) kodu değişmedi.

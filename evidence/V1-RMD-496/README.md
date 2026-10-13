# V1-RMD-496 - Müşteri hesabı doğruluk düzeltmeleri

## Kurallar

- Müşteri listesinde arama terimi verilirse en yeni 5000 profil taranır (eskiden yalnız en yeni 500); böylece 500'den eski müşteri aranınca bulunur ve hesaba borç yazılabilir. Terimsiz liste en yeni 500 ile sınırlı kalır (profil alanları şifreli olduğu için arama çözümleme yapar; sunucu tarafı şifreli arama dizini kapsam dışı).
- Hesaba yazma tutarı en çok iki ondalıklı olabilir. Eskiden 10,004 gibi tutar kredi politikasına ham haliyle girip defterde 10,00 olarak yazılıyor, aynı anahtarla yeniden gönderimde `IDEMPOTENCY_KEY_REUSED` veriyordu. Artık doğrulama hatasıyla reddedilir, hiçbir şey yazılmaz.

## Kanıt

- `tests-module.log`: 24 test (yeni: ikiden fazla ondalık reddi). `tests-host.log`: 20 test, gerçek Host + Postgres (yeni: 500'den eski müşteri aramada bulunur, düz liste sınırlı kalır).
- `mutation.log`: 3 mutant; dosyalar hash ile geri yüklendi.
- Gerçek-Host denemesi ayrıca yazılmadı: Host testleri (`CustomerAccountsHttpTests`) gerçek Host kapsayıcısı ve gerçek Postgres üzerinden HTTP ile çalışır.

# V1-RMD-430 - kabul kanıtı

Görev dosyasındaki kabul ölçütlerinin karşılandığını gösteren özet; komut çıktıları bu klasördedir.

- `dotnet test tests/Architecture/ApiConventions` exit code 0 (5/5, gerçek Host ve PostgreSQL 18):
  `evidence/V1-RMD-430/tests.log`.
- Test, çalışan Host'un uç listesini okur; POST, PUT, PATCH ve DELETE uçlarında gövdede `IdempotencyKey` alanı ya da
  `X-Idempotency-Key` / `Idempotency-Key` başlık parametresi arar. Bugün anahtarsız 191 uç (149 POST, 26 PUT,
  14 DELETE, 2 PATCH) `idempotency_allowlist.json` içinde gerekçe ve ucu tanımlayan dosyanın plan sahibi görevle
  kayıtlı; anahtar eklenen ya da kaldırılan bir uç listede kalırsa test kırılır.
- Mutasyon kontrolü: anahtarsız geçici bir POST ucu testi kırmızı yapar; aynı uca gövde anahtarı ya da
  `X-Idempotency-Key` başlığı eklenince yeşile döner; uç kaldırılınca yeşil (`evidence/V1-RMD-430/mutation.log`).
- Semih için senaryo: yeni bir veri değiştiren uç tekrar anahtarı olmadan eklenirse bu test hangi ucun eksik
  olduğunu söyleyerek kırılır; listedeki uçlara anahtar eklendikçe liste kısalır.

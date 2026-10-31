# V1-RMD-763 sonuç

Bulgu V1-IAM-013: `IssuedSessionToken` düz `record` olduğundan `ToString()` canlı oturum belirtecini yazıyordu; `LoginSuccess` bunu taşıdığı için günlük veya istisna metni belirteci sızdırabilirdi.

Düzeltme: `IssuedSessionToken.ToString()` yalnız bitiş zamanını yazar (belirteç ve özet yazılmaz).

## Kanıt
- Kırmızı: `red-with-db/` (gerçek PostgreSQL ortamıyla): iki yeni test düştü, 61 geçti. `red/` ilk koşudur: PostgreSQL ortam değişkenleri yoktu, ek olarak 58 veritabanı testi bağlantı kurulamadığı için düştü (60 başarısız; toplam sayı diğer koşulardan farklı, farkın nedeni araştırılmadı); bu yüzden `red-with-db/` esas alınır.
- Yeşil (`green/`): `ALKAROS.Identity.Authentication.Tests` 63/63, exit 0.
- Mutasyon (`mutation-no-override/`): `ToString` geçersiz kılması kaldırılınca iki test düştü (derlenir mutant; ilk deneme sözdizimi hatası verdiği için atıldı). Kaynak `cmp` ile geri yüklendi.

## Sınırlar
- Yalnız `IssuedSessionToken` ve onu taşıyan `LoginSuccess` metni kapsanır; `SensitivePayload` değişmezliği (V1-SEC-005) ve belirteci başka yoldan günlüğe yazan kod ayrı konudur.

## İnceleme
- Güvenlik/kod ve test/kanıt incelemesi, engelleyici bulgu yok. Düzeltilen kusur: `red/` başarısızlık sayısı. Doğrulanamayan: ilk (söz dizimi hatalı) mutasyon denemesinin kaydı tutulmadı.
- Kapsam: `scope-vs-b726092a.json` valid=true.
- Benzer sızıntı sınıfı (varsayılan `ToString` düz sır basan kayıtlar; `LoginRequest`, `UnlockRequest`, `PairingRequestCreated`, `CustomerSessionIssueResult`, `CreateUserRequestV1`, `SaveQnbCredentialHttpRequest`, `SaveRelayCredentialRequest` vb.) bu görevin dışındadır; ayrı görevle ele alınır.

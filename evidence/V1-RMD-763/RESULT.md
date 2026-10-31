# V1-RMD-763 sonuç

Bulgu V1-IAM-013: `IssuedSessionToken` düz `record` olduğundan `ToString()` canlı oturum belirtecini yazıyordu; `LoginSuccess` bunu taşıdığı için günlük veya istisna metni belirteci sızdırabilirdi.

Düzeltme: `IssuedSessionToken.ToString()` yalnız bitiş zamanını yazar (belirteç ve özet yazılmaz).

## Kanıt
- Kırmızı: `red-with-db/` (gerçek PostgreSQL ortamıyla): iki yeni test düştü, 61 geçti. `red/` ilk koşudur: PostgreSQL ortam değişkenleri yoktu, ek olarak 3 veritabanı testi ortam eksikliğinden düştü; bu yüzden `red-with-db/` esas alınır.
- Yeşil (`green/`): `ALKAROS.Identity.Authentication.Tests` 63/63, exit 0.
- Mutasyon (`mutation-no-override/`): `ToString` geçersiz kılması kaldırılınca iki test düştü (derlenir mutant; ilk deneme sözdizimi hatası verdiği için atıldı). Kaynak `cmp` ile geri yüklendi.

## Sınırlar
- Yalnız `IssuedSessionToken` ve onu taşıyan `LoginSuccess` metni kapsanır; `SensitivePayload` değişmezliği (V1-SEC-005) ve belirteci başka yoldan günlüğe yazan kod ayrı konudur.

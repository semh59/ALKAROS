# Login Timing Equality Contract (V1-IAM-005)

Kaynak: FIND-IA-0056, FIND-IA-0057 (bağımsız denetim), CORR:C42.
Uygulama: `src/Modules/Identity/Authentication/AuthenticationService.cs`,
`PasswordHasher.cs`. Test: `tests/Modules/Identity/Authentication/
AuthenticationTimingContractTests.cs`.

## Amaç

Login yanıt süresi, kullanıcı adının var olup olmadığını veya hesabın aktif
olup olmadığını ele vermez. Süre eşitliği stopwatch ölçümüyle değil, her yolun
ürettiği işin yapısal olarak aynı olmasıyla garanti edilir.

## Zorunlu iş sözleşmesi

| Yol | PBKDF2 doğrulaması | DB yazımı |
| --- | --- | --- |
| Bilinmeyen kullanıcı adı | Tam olarak 1; `DummyHash` (sabit 600k = `DefaultIterations`) | 0 |
| Bilinen ama inaktif kullanıcı | Tam olarak 1; `DummyHash` | 0 |
| Bilinen aktif, kilitli kullanıcı | 0 | 0 |
| Bilinen aktif, yanlış parola | Tam olarak 1; kullanıcının saklı hash'inin kendi iteration sayısı | Tam olarak 1 atomik failure-counter yazımı |
| Bilinen aktif, doğru parola | Tam olarak 1; kullanıcının saklı hash'inin kendi iteration sayısı | Tam olarak 1 success yazımı |

- Doğrulama maliyeti yalnız saklı hash'in kendi work factor'ına bağlıdır;
  kullanıcı adının varlığına veya hesap durumuna asla bağlı değildir.
- Bilinmeyen/inaktif yolda ekstra PBKDF2 işi veya başka bir yan etki üretilmez.
- Kilitli yolda ek PBKDF2 işi üretilmez; red, doğrulamadan önce döner.
- Yanlış parola yolunda ek PBKDF2 işi üretilmez; tek ek iş atomik failure
  yazımıdır.

## Work factor sınırları

- Saklı hash iteration sayısı `MinimumIterations` (10.000) ile
  `MaximumIterations` (2.000.000) arasındadır; dışındaki veya bozuk kayıt
  doğrulama öncesi reddedilir (sınırsız PBKDF2 yükü = DoS önlenir).
- `DummyHash` iteration sayısı `DefaultIterations` (600.000) ile birebir
  eşittir; sözleşme testi bu eşitliği ve dummy hash'in gerçekliğini kilitler.
  `DefaultIterations` değişirse dummy hash de yeniden üretilmek zorundadır.

## Work factor yakınsama politikası

- Kabul edilen kullanıcı hash'leri `DefaultIterations`'a yakınsar (rehash-on-
  login). Store katmanı uygulaması `IUserStore` sözleşme uzantısı
  (`UpdatePasswordHashAsync`) gerektirir; `IUserStore.cs` ve
  `PostgresUserStore.cs` V1-IAM-004 sahipliğindedir, bu yüzden yakınsama
  uygulaması ayrı kullanıcı onaylı plan değişikliğiyle yüzey devri yapılmadan
  yazılamaz.
- Yakınsama öncesi, default altı legacy hash'li bir hesabın yanlış-parola
  yolunun işi dummy yoldan düşüktür; bu pencere hesabın ilk başarılı login'i
  ile kapanır. Yeni kayıtlar her zaman `DefaultIterations` ile üretilir.

## Kanıt disiplini

- Zamanlama ölçümü kabul kanıtı değildir. Kararlı kanıt yapısaldır:
  testler inject edilen verifier (delegate seam) ile her yolun PBKDF2
  doğrulama sayısını, DB yazım davranışını ve work factor sınırlarını
  deterministik sayar; stopwatch eşik testi kullanılmaz.
- Login akışındaki verifier varsayılanı `PasswordHasher.Verify`'dır;
  üretim davranışı değişmez, yalnız sözleşme test edilebilir hale gelir.

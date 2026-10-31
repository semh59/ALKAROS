# V1-RMD-767 sonuç

Bulgu V1-SEC-005: `SensitivePayload.Fields/Categories` ve `SensitiveEnvelope.FieldCategories` salt-okunur arayüz arkasında düz `Dictionary` tutuyordu; aşağı çevirmeyle kategori sonradan değiştirilebilirdi (örn. `Payment` alanı `Public` yapılıp maskeleme atlanır).

Düzeltme: sözlükler kopyalandıktan sonra `ReadOnlyDictionary` ile sarılır.

## Kanıt
- Kırmızı (`red/`): iki aşağı çevirme testi düştü, 37 geçti. Üçüncü yeni test (kaynak sözlük sonradan değişir) zaten geçiyordu: kopya önceden yapılıyordu; bu test kopyanın kaldırılmasına karşı koruma amaçlıdır, bu yüzden kırmızı üretmez ve o yönden mutasyonla sınanmadı.
- Yeşil (`green/`): `ALKAROS.SensitiveData.Tests` 39/39, exit 0.
- Mutasyon: yük sarmalayıcısı geri alınınca (`mutation-payload-unwrapped/`) yalnız yük testi, zarf sarmalayıcısı geri alınınca (`mutation-envelope-unwrapped/`) yalnız zarf testi düştü. Kaynaklar `cmp` ile geri yüklendi.
- Kod tabanında bu sözlüklere `Dictionary` aşağı çevirmesi yapan başka yer yok (grep).

## Sınırlar
- `SensitiveEnvelope` üzerindeki `init` erişimcileri ve `with` ifadesiyle yeniden atama (kopya ve sarma atlanır) kapsam dışıdır; değişmezlik kısmen sağlanır.
- Kopyanın kaldırılmasına karşı mutasyon yapılmadı.

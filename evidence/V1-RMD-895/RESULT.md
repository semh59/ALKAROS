# V1-RMD-895 sonucu

Yaşlandırma raporu uç noktası artık müşteri adlarını tek profil okumasıyla alıyor. Eskiden her müşteri için `CustomerAccountsStore.GetAsync` çağrılıyordu (hesap satırı sorgusu + şifreli profil sorgusu, yani müşteri başına 2 tarama); 100.000 müşteriye kadar çıkabilen rapor için bu 200.000 sorgudur.

## Değişiklik
- `ICustomerProfileStore.GetManyAsync` (tek sorgu, `customer_id = ANY(...)`, rol projeksiyonu aynı); `GetAsync` artık aynı yolu tek kimlikle kullanıyor (ayrı kod yolu yok). Var olmayan kimlik sonuçta yok.
- `CustomerAccountsStore.NamesAsync`: anonimleştirilmiş profili dışarıda bırakır, adı olmayanı "İsimsiz müşteri" yapar (sabit iki yerde ortak).
- Rapor uç noktası adları tek çağrıyla alır; profili okunamayan/anonimleşmiş müşteri eskisi gibi "Profili okunamayan müşteri".

## Kanıt
- Kırmızı: red-1.log (12 müşterilik raporda profil tablosu 24 kez tarandı). Sayım Postgres istatistiğinden: sunucunun boştaki bağlantıları kapatılır (arka uç çıkarken istatistiğini yazar), sonra `pg_stat_user_tables` okunur.
- Yeşil: profil deposu 62/62 (final-profiles.log), anonimleştirme 36/36, yaşlandırma sınıfı 8/8 (final-aging.log), tüm CustomerAccounts host projesi 33/33 (green-host.log, 3 dk 21 sn). Uç nokta artık ≤2 tarama yapıyor (testin sınırı).
- Mutasyon (mutations.txt): müşteri başına okuma, anonimleşmiş adlandırma, adlar yok sayma, `LIMIT 1`, rol projeksiyonu yok sayma, anonimleşmişi çözme: 6/6 öldü, cmp ile geri alındı.

## Sınır
- Rapor hâlâ en çok 100.000 müşteri döndürür (883 sınırı); 100.000 kimlikli tek `ANY` dizisi tek parametredir, ayrıca sayfalama yok. Profil çözümü müşteri başına şifre çözmedir (CPU); sorgu sayısı artık sabit.
- Kasa müşteri listesi (`ListAsync`) ve arama bu görevde değişmedi (kendi üst sınırları var: 500/5000).

## İnceleme sonrası (tur 1)
- Hesap satırı davranışı aynı: `ReadAccountRowsAsync` hesap satırını doğrudan `customer_data.profiles` tablosundan (anonimleşmemiş profil) kurar; eski yolda "satır var" ile "profil var ve anonimleşmemiş" aynı koşuldu, yeni yol aynı koşulu uygular.
- Test sayımı artık `1 <= tarama <= 2` doğrular (ölçüm çalışmazsa yanlış geçmez).
- Bellek: 100.000 müşteride çözülmüş profiller (ad, telefon, e-posta, adres) aynı anda bellekte durur (eskiden tek tek); parçalama bilinçli yapılmadı çünkü istenen tek sorgudur ve sınır 100.000 müşteridir. Gerekirse `NamesAsync` içinde parçalanır.
- Birleşik denetimde SELECT sınırı zorunlu çıktı: `GetManyAsync` sorgusuna `LIMIT` = istenen kimlik sayısı eklendi.

# V1-RMD-765 sonuç

Bulgu V1-RMD-130: köprü sorgusu `created_at` sırasıyla ilk 50 bileti süzgeçsiz seçiyordu. (1) Etkin yazıcısı olmayan istasyonun biletleri hiç iş almadığı için her turda aynı en eski 50 bilet aday kalıyor, yazıcılı istasyonların yeni biletleri köprülenmiyordu. (2) Sonradan yazıcı tanımlanınca o istasyonun geçmişteki hazır biletleri toplu basılırdı.

Düzeltme: sorgu yalnız etkin yazıcısı olan istasyonun, hazırlanmakta olan (`Queued`, `Accepted`, `Preparing`) biletlerini seçer.

## Kanıt
- Kırmızı (`red/`): proje derlendikten sonra iki yeni test düştü, mevcut 3 test geçti (2 başarısız, 3 başarılı). `not-red-build-failed/` derleme hatası (V1-RMD-766 öncesi) nedeniyle koşmamış ilk denemedir, kanıt sayılmaz.
- Yeşil (`green/`): 5/5, exit 0. Tam KitchenOperations paketi (`green-full/`): 73/73, exit 0.
- Mutasyon: yazıcı süzgeci kaldırılınca (`mutation-no-printer-filter/`) yalnız yazıcı testi düştü; durum süzgeci gevşetilince (`mutation-no-status-filter/`) yalnız hazır-bilet testi düştü. Kaynak her seferinde `cmp` ile geri yüklendi.

## Sınırlar
- Yeni bir ürün kuralı eklenmedi: "hazır bilet köprülenmez" kararı, hazır bir biletin kâğıdının işe yaramayacağı varsayımına dayanır. Mutfak biletleri çok hızlı hazır oluyorsa (köprü aralığı 5 sn) ilk baskı kaçabilir; bu görevde ele alınmadı.
- Zaman penceresi (bilet yaşı) eklenmedi.
- V1-KIT-003/004 (kurtarma, çökme koruması) kapsam dışı.

## İnceleme
- Kod/ürün riski ve test/kanıt incelemesi, engelleyici bulgu yok. Bilet durum kısıtı ve enum aynı beş değeri taşır; yazıcı süzgeci köprünün sonraki adımıyla (Ordinal eşleşme) tutarlıdır; mevcut indeksler yeterlidir.
- Ürün riski (kodda hata değil): bilet `Queued` başlar, `Ready`'ye yalnız `Preparing`'den ya da tüm kalemler hazır olunca geçer; köprü veya yazıcı kapalıyken oluşan, ya da 5 sn içinde bitirilen bilet basılmaz. Gerçekçi bir yol sayılmadı; karar ürün sahibinindir.
- Test notları (düşük): starvation testi "yazıcı yok" yerine "yazıcı pasif" kurar (SQL ikisini de kapsar); "Cancelled bilet köprülenmez" için ayrı test yoktur; mutant diff'leri ve `cmp` çıktısı kanıt klasörüne kaydedilmedi (geri yükleme `git diff HEAD` ile temiz görüldü).
- Kapsam: `scope-vs-9292f95e.json` valid=true.

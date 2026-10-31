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
